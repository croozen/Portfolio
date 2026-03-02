"""Main entry point for the AI - Agent001 application.  This is the start of a framework."""

import asyncio
import logging
#import debugpy
from dotenv import load_dotenv 
from agent import AIAgent001 #, AgentConfig

#debugpy.listen(5678)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
) 
logger = logging.getLogger(__name__)

async def main():
    """Main function to initialize and run the AI agent.
    """
    # Load environment variables from .env file
    load_dotenv()

    # Load configuration from environment variables
    logger.info("Starting the AI Agent001...")

    # Initialize the agent with the loaded configuration
    agent= AIAgent001()
    logger.info(f"Initialized {agent.name} with description: {agent.description}")
    
    user_input = input("Tell me an object so I can write a haiku about it: ")
    logger.info(f"User Input: {user_input}")

    # Run the agent's main loop
    response = agent.process_input(user_input)
    logger.info(f"Agent Response: {response}")


if __name__ == "__main__":
    asyncio.run(main())