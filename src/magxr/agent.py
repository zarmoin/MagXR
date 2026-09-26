import io
import time
import uuid
from typing import TypedDict, Optional, List, Dict, Any, Union
from pydantic import BaseModel, Field

from langchain_core.tools import tool
from langgraph.graph import StateGraph, START, END
from langgraph.checkpoint.memory import MemorySaver
from langgraph.types import interrupt, Command

from src.magxr.prompt_parser import (
    MagnificationPrompt,
    parse_magnification_prompt,
    transcribe_audio,
    process_audio_command
)

# ---------------------------------------------------------------------------
# Step 4 Action Item 3: Custom Tools matching Unity Payloads (Step 5)
# ---------------------------------------------------------------------------

SPEED_DURATION_MAP: Dict[str, float] = {
    "fast": 0.2,
    "normal": 0.5,
    "slow": 1.0,
}


@tool
def set_magnifier_zoom(factor: float = 2.0, speed: str = "normal") -> Dict[str, Any]:
    """
    Sets the stereoscopic magnifier zoom factor (bounded between 1.0 and 10.0)
    with specified transition speed ('slow', 'normal', 'fast').
    """
    bounded_factor = max(1.0, min(10.0, float(factor)))
    speed_normalized = speed.lower() if speed.lower() in SPEED_DURATION_MAP else "normal"
    duration = SPEED_DURATION_MAP[speed_normalized]

    return {
        "action": "SET_ZOOM",
        "factor": round(bounded_factor, 2),
        "speed": speed_normalized,
        "duration": duration
    }


@tool
def move_magnifier_to_target(target_object: str, speed: str = "normal") -> Dict[str, Any]:
    """
    Moves the virtual loupe or camera focus to a target scene object
    (e.g., 'red cube', 'blue cube', 'high resolution map plane').
    """
    speed_normalized = speed.lower() if speed.lower() in SPEED_DURATION_MAP else "normal"
    duration = SPEED_DURATION_MAP[speed_normalized]

    return {
        "action": "MOVE_TO_TARGET",
        "target": target_object.strip(),
        "speed": speed_normalized,
        "duration": duration
    }


@tool
def create_checkpoint(checkpoint_name: str = "Checkpoint") -> Dict[str, Any]:
    """
    Creates a spatial view checkpoint saving current camera position,
    magnification level, and target bounds.
    """
    return {
        "action": "CREATE_CHECKPOINT",
        "name": checkpoint_name.strip() if checkpoint_name else "Checkpoint",
        "timestamp": time.time()
    }


@tool
def reset_magnification(speed: str = "normal") -> Dict[str, Any]:
    """
    Resets the magnification level back to 1.0x default view.
    """
    speed_normalized = speed.lower() if speed.lower() in SPEED_DURATION_MAP else "normal"
    duration = SPEED_DURATION_MAP[speed_normalized]

    return {
        "action": "RESET_ZOOM",
        "factor": 1.0,
        "speed": speed_normalized,
        "duration": duration
    }


# List of all available custom tools
CUSTOM_TOOLS = [
    set_magnifier_zoom,
    move_magnifier_to_target,
    create_checkpoint,
    reset_magnification
]

# ---------------------------------------------------------------------------
# State Definition
# ---------------------------------------------------------------------------

class MagXRState(TypedDict, total=False):
    # User Input
    input_text: str
    audio_transcript: Optional[str]
    parsed_prompt: Optional[Dict[str, Any]]
    
    # Tool Planning
    planned_tools: List[str]
    remaining_tools: List[str]
    tool_args: Dict[str, Dict[str, Any]]
    
    # Human-in-the-Loop Confirmation
    confirmation_payload: Optional[Dict[str, Any]]
    confirmed: Optional[bool]
    
    # Outputs destined for Unity (Step 5)
    execution_results: List[Dict[str, Any]]
    unity_payloads: List[Dict[str, Any]]
    
    # State tracking
    status: str  # "planning", "awaiting_confirmation", "executing", "completed", "cancelled", "error"
    error_message: Optional[str]


# ---------------------------------------------------------------------------
# Graph Nodes
# ---------------------------------------------------------------------------

def plan_node(state: MagXRState) -> Dict[str, Any]:
    """
    Analyzes user input or parsed prompt and plans the sequence of tool nodes.
    Constructs the confirmation payload for the Unity World-Space Canvas.
    """
    input_text = state.get("input_text", "")
    parsed_dict = state.get("parsed_prompt")
    
    # If not already parsed into MagnificationPrompt, parse using Step 3 LLM
    if not parsed_dict:
        if input_text:
            try:
                parsed_prompt = parse_magnification_prompt(input_text)
                parsed_dict = parsed_prompt.model_dump()
            except Exception as e:
                # Fallback rule-based parsing if offline
                parsed_dict = {
                    "direction": "reset" if "reset" in input_text.lower() else "in",
                    "zoom_factor": 2.0,
                    "zooming_speed": "normal",
                    "target_object": None,
                    "transcript": input_text
                }
        else:
            parsed_dict = {
                "direction": "reset",
                "zoom_factor": 1.0,
                "zooming_speed": "normal",
                "target_object": None,
                "transcript": ""
            }

    direction = parsed_dict.get("direction", "in")
    zoom_factor = float(parsed_dict.get("zoom_factor", 1.0))
    speed = parsed_dict.get("zooming_speed", "normal")
    target_object = parsed_dict.get("target_object")
    transcript = parsed_dict.get("transcript", input_text)

    planned_tools: List[str] = []
    tool_args: Dict[str, Dict[str, Any]] = {}

    # Check for spatial checkpoint command
    if "checkpoint" in transcript.lower():
        planned_tools.append("create_checkpoint")
        cp_name = target_object if target_object else "Checkpoint"
        tool_args["create_checkpoint"] = {"checkpoint_name": cp_name}

    # Reset command
    elif direction == "reset":
        planned_tools.append("reset_magnification")
        tool_args["reset_magnification"] = {"speed": speed}

    # Zoom / Move command
    else:
        # If target object is specified, move to it first
        if target_object:
            planned_tools.append("move_magnifier_to_target")
            tool_args["move_magnifier_to_target"] = {
                "target_object": target_object,
                "speed": speed
            }
        
        # If zoom factor is specified or direction is in/out
        if zoom_factor > 1.0 or direction in ("in", "out"):
            planned_tools.append("set_magnifier_zoom")
            tool_args["set_magnifier_zoom"] = {
                "factor": zoom_factor,
                "speed": speed
            }

    # Fallback to reset if no tool was planned
    if not planned_tools:
        planned_tools.append("reset_magnification")
        tool_args["reset_magnification"] = {"speed": "normal"}

    # Formulate human-readable summary for VR world-space confirmation canvas
    summary_parts = []
    if "move_magnifier_to_target" in planned_tools:
        summary_parts.append(f"Move focus to '{target_object}'")
    if "set_magnifier_zoom" in planned_tools:
        summary_parts.append(f"Zoom {direction.upper()} to {zoom_factor}x ({speed} speed)")
    if "create_checkpoint" in planned_tools:
        summary_parts.append(f"Save spatial checkpoint '{tool_args['create_checkpoint']['checkpoint_name']}'")
    if "reset_magnification" in planned_tools:
        summary_parts.append(f"Reset view to default 1.0x ({speed} speed)")

    summary = " & ".join(summary_parts) if summary_parts else "Execute magnification update"

    confirmation_payload = {
        "type": "CONFIRMATION_REQUEST",
        "summary": summary,
        "planned_tools": planned_tools,
        "direction": direction,
        "zoom_factor": zoom_factor,
        "speed": speed,
        "target_object": target_object,
        "transcript": transcript,
        "status": "awaiting_confirmation"
    }

    return {
        "parsed_prompt": parsed_dict,
        "planned_tools": list(planned_tools),
        "remaining_tools": list(planned_tools),
        "tool_args": tool_args,
        "confirmation_payload": confirmation_payload,
        "unity_payloads": [],
        "execution_results": [],
        "status": "awaiting_confirmation"
    }


def human_confirmation_node(state: MagXRState) -> Dict[str, Any]:
    """
    Step 4 Action Item 1: Equipped with interrupt() node that pauses execution
    and emits the parsed instruction to Unity for human-in-the-loop confirmation.
    Step 4 Action Item 2: Resumed via Command(resume={"confirmed": True/False}).
    """
    confirmation_payload = state.get("confirmation_payload", {})
    
    # Pauses graph execution and yields confirmation_payload to caller / Unity
    resume_val = interrupt(confirmation_payload)
    
    # Resumed: extract user response
    is_confirmed = False
    if isinstance(resume_val, dict):
        is_confirmed = bool(resume_val.get("confirmed", False))
    elif isinstance(resume_val, bool):
        is_confirmed = resume_val

    return {
        "confirmed": is_confirmed,
        "status": "executing" if is_confirmed else "cancelled"
    }


def route_confirmation(state: MagXRState) -> str:
    """
    Branches graph based on user confirmation in headset.
    """
    if state.get("confirmed"):
        return "dispatcher"
    return "cancel"


def dispatcher_node(state: MagXRState) -> Dict[str, Any]:
    """
    Hub that routes to remaining tool nodes sequentially.
    """
    return {}


def route_dispatcher(state: MagXRState) -> str:
    """
    Selects the next tool from remaining_tools or exits to END.
    """
    remaining = state.get("remaining_tools", [])
    if not remaining:
        return END
    next_tool = remaining[0]
    return next_tool


# --- Step 4 Action Item 3: Custom Tool Nodes ---

def node_set_magnifier_zoom(state: MagXRState) -> Dict[str, Any]:
    remaining = list(state.get("remaining_tools", []))
    if "set_magnifier_zoom" in remaining:
        remaining.remove("set_magnifier_zoom")

    args = state.get("tool_args", {}).get("set_magnifier_zoom", {})
    result = set_magnifier_zoom.invoke(args)

    unity_payloads = list(state.get("unity_payloads", [])) + [result]
    execution_results = list(state.get("execution_results", [])) + [result]

    return {
        "remaining_tools": remaining,
        "unity_payloads": unity_payloads,
        "execution_results": execution_results,
        "status": "completed" if not remaining else "executing"
    }


def node_move_magnifier_to_target(state: MagXRState) -> Dict[str, Any]:
    remaining = list(state.get("remaining_tools", []))
    if "move_magnifier_to_target" in remaining:
        remaining.remove("move_magnifier_to_target")

    args = state.get("tool_args", {}).get("move_magnifier_to_target", {})
    result = move_magnifier_to_target.invoke(args)

    unity_payloads = list(state.get("unity_payloads", [])) + [result]
    execution_results = list(state.get("execution_results", [])) + [result]

    return {
        "remaining_tools": remaining,
        "unity_payloads": unity_payloads,
        "execution_results": execution_results,
        "status": "completed" if not remaining else "executing"
    }


def node_create_checkpoint(state: MagXRState) -> Dict[str, Any]:
    remaining = list(state.get("remaining_tools", []))
    if "create_checkpoint" in remaining:
        remaining.remove("create_checkpoint")

    args = state.get("tool_args", {}).get("create_checkpoint", {})
    result = create_checkpoint.invoke(args)

    unity_payloads = list(state.get("unity_payloads", [])) + [result]
    execution_results = list(state.get("execution_results", [])) + [result]

    return {
        "remaining_tools": remaining,
        "unity_payloads": unity_payloads,
        "execution_results": execution_results,
        "status": "completed" if not remaining else "executing"
    }


def node_reset_magnification(state: MagXRState) -> Dict[str, Any]:
    remaining = list(state.get("remaining_tools", []))
    if "reset_magnification" in remaining:
        remaining.remove("reset_magnification")

    args = state.get("tool_args", {}).get("reset_magnification", {})
    result = reset_magnification.invoke(args)

    unity_payloads = list(state.get("unity_payloads", [])) + [result]
    execution_results = list(state.get("execution_results", [])) + [result]

    return {
        "remaining_tools": remaining,
        "unity_payloads": unity_payloads,
        "execution_results": execution_results,
        "status": "completed" if not remaining else "executing"
    }


def cancel_node(state: MagXRState) -> Dict[str, Any]:
    """
    Handles user cancellation: emits a cancellation payload and halts tool execution.
    """
    cancel_payload = {
        "action": "CONFIRMATION_CANCELLED",
        "reason": "User rejected confirmation in VR",
        "timestamp": time.time()
    }
    return {
        "remaining_tools": [],
        "unity_payloads": [cancel_payload],
        "status": "cancelled"
    }


# ---------------------------------------------------------------------------
# Graph Construction
# ---------------------------------------------------------------------------

def build_magxr_graph(checkpointer: Optional[MemorySaver] = None):
    """
    Constructs the LangGraph state machine with human confirmation interrupt
    and custom tool nodes.
    """
    if checkpointer is None:
        checkpointer = MemorySaver()

    builder = StateGraph(MagXRState)

    # Add all nodes
    builder.add_node("plan", plan_node)
    builder.add_node("human_confirmation", human_confirmation_node)
    builder.add_node("dispatcher", dispatcher_node)
    builder.add_node("set_magnifier_zoom", node_set_magnifier_zoom)
    builder.add_node("move_magnifier_to_target", node_move_magnifier_to_target)
    builder.add_node("create_checkpoint", node_create_checkpoint)
    builder.add_node("reset_magnification", node_reset_magnification)
    builder.add_node("cancel", cancel_node)

    # Define edges
    builder.add_edge(START, "plan")
    builder.add_edge("plan", "human_confirmation")

    # Conditional edge after human confirmation
    builder.add_conditional_edges(
        "human_confirmation",
        route_confirmation,
        {
            "dispatcher": "dispatcher",
            "cancel": "cancel"
        }
    )

    # Conditional edge from dispatcher to next tool or END
    builder.add_conditional_edges(
        "dispatcher",
        route_dispatcher,
        {
            "set_magnifier_zoom": "set_magnifier_zoom",
            "move_magnifier_to_target": "move_magnifier_to_target",
            "create_checkpoint": "create_checkpoint",
            "reset_magnification": "reset_magnification",
            END: END
        }
    )

    # Tool nodes loop back to dispatcher
    builder.add_edge("set_magnifier_zoom", "dispatcher")
    builder.add_edge("move_magnifier_to_target", "dispatcher")
    builder.add_edge("create_checkpoint", "dispatcher")
    builder.add_edge("reset_magnification", "dispatcher")
    builder.add_edge("cancel", END)

    return builder.compile(checkpointer=checkpointer)


# ---------------------------------------------------------------------------
# High-Level Orchestrator API
# ---------------------------------------------------------------------------

class MagXROrchestrator:
    """
    Agent Orchestrator providing high-level methods to start commands,
    receive confirmation interrupts, and resume execution.
    """

    def __init__(self, checkpointer: Optional[MemorySaver] = None):
        self.checkpointer = checkpointer or MemorySaver()
        self.graph = build_magxr_graph(self.checkpointer)

    def start_command(
        self,
        input_command: Union[str, MagnificationPrompt, io.BytesIO, bytes],
        thread_id: Optional[str] = None
    ) -> Dict[str, Any]:
        """
        Runs the graph up to the human_confirmation interrupt().
        Returns the confirmation payload destined for the Unity World-Space Canvas.
        """
        thread_id = thread_id or f"session-{uuid.uuid4().hex[:8]}"
        config = {"configurable": {"thread_id": thread_id}}

        initial_state: Dict[str, Any] = {}

        if isinstance(input_command, MagnificationPrompt):
            initial_state["parsed_prompt"] = input_command.model_dump()
            initial_state["input_text"] = input_command.transcript or ""
        elif isinstance(input_command, (io.BytesIO, bytes)):
            transcript, prompt = process_audio_command(input_command)
            initial_state["parsed_prompt"] = prompt.model_dump()
            initial_state["input_text"] = transcript
            initial_state["audio_transcript"] = transcript
        else:
            initial_state["input_text"] = str(input_command)

        # Execute up to interrupt
        result = self.graph.invoke(initial_state, config=config)

        # Extract interrupt value if paused
        interrupt_val = None
        if "__interrupt__" in result and result["__interrupt__"]:
            interrupt_val = result["__interrupt__"][0].value
        else:
            interrupt_val = result.get("confirmation_payload")

        return {
            "thread_id": thread_id,
            "status": result.get("status", "awaiting_confirmation"),
            "confirmation_payload": interrupt_val or result.get("confirmation_payload"),
            "raw_result": result
        }

    def confirm_command(
        self,
        thread_id: str,
        confirmed: bool = True
    ) -> Dict[str, Any]:
        """
        Resumes the paused graph using Command(resume={"confirmed": confirmed}).
        If confirmed: executes planned tool nodes and returns Unity payloads.
        If cancelled: marks state as cancelled.
        """
        config = {"configurable": {"thread_id": thread_id}}
        resume_command = Command(resume={"confirmed": confirmed})

        resumed_state = self.graph.invoke(resume_command, config=config)

        return {
            "thread_id": thread_id,
            "status": resumed_state.get("status"),
            "confirmed": resumed_state.get("confirmed"),
            "unity_payloads": resumed_state.get("unity_payloads", []),
            "execution_results": resumed_state.get("execution_results", [])
        }

    def run_with_auto_confirm(
        self,
        input_command: Union[str, MagnificationPrompt, io.BytesIO, bytes],
        confirmed: bool = True,
        thread_id: Optional[str] = None
    ) -> Dict[str, Any]:
        """
        Convenience execution helper that starts the command and immediately resumes.
        Useful for testing and automated evaluation trials.
        """
        start_res = self.start_command(input_command, thread_id=thread_id)
        thread_id = start_res["thread_id"]
        return self.confirm_command(thread_id, confirmed=confirmed)
