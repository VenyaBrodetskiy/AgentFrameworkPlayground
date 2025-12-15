import asyncio
from pathlib import Path
from typing import Annotated, Any

from agent_framework import ai_function
from agent_framework.azure import AzureOpenAIChatClient
from pydantic import Field

from config_helpers import load_json, require_str, resolve_appsettings_paths

@ai_function(name="get_weather", description="Get the weather for a given location.")
def get_weather(
    location: Annotated[str, Field(description="The location to get the weather for.")],
) -> str:
    return f"The weather in {location} is cloudy with a high of 15°C."


async def main() -> None:
    print("=== Running: AgentFrameworkPython ===")

    here = Path(__file__).resolve().parent

    config = {}
    base_path, dev_path = resolve_appsettings_paths(here)
    if base_path is not None:
        config |= load_json(base_path)
    config |= load_json(dev_path)

    model_name = require_str(config, "ModelName")
    endpoint = require_str(config, "Endpoint")
    api_key = require_str(config, "ApiKey")

    client = AzureOpenAIChatClient(
        endpoint=endpoint,
        deployment_name=model_name,
        api_key=api_key,
    )

    agent = client.create_agent(
        instructions="say 'just a second' before answering question",
        tools=[get_weather],
        name="myagent",
    )

    thread = agent.get_new_thread()

    while True:
        try:
            user_input = input("user> ").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            break

        if not user_input:
            continue

        print("agent> ", end="", flush=True)
        async for update in agent.run_stream(user_input, thread=thread):
            if update.text:
                print(update.text, end="", flush=True)
        print()


if __name__ == "__main__":
    asyncio.run(main())
