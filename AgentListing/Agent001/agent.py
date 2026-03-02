import os

from openai import OpenAI

class AIAgent001:
    def __init__(self):
        self.name = "Agent001"
        self.description = "Agent001 is an AI agent designed to respond to an input with a haiku that is about the input given using OpenAI's API."
        self.client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))
        
        self.history = [
            {"role": "system", "content": "You are an AI agent designed to respond to an input with a haiku that is about the input given using OpenAI's API."},
            {"role": "user", "content": "Dragonfruit"}
        ]

    def process_input(self, user_input: str) -> str:   
        # Validate input
        if not isinstance(user_input, str):
            raise TypeError("user_input must be a string.")
        if not user_input.strip():
            return "I didn't catch that. Could you say it again?"

        # Add user message to history
        self.history.append({"role": "user", "content": user_input.strip()})     
        try:
            response = self.client.chat.completions.create(
                model=os.getenv("MODEL_NAME", "gpt-4o-mini"),
                messages=self.history,
                temperature=0.7, #Creativity level
                max_tokens=300 #limit the response to 100 tokens
            )

            # Extract the AI's reply and update the conversation history
            ai_reply = response.choices[0].message.content.strip()

            # Add the AI's reply to the conversation history
            self.history.append({"role": "assistant", "content": ai_reply})

            return ai_reply
        except Exception as e:
            return f"Error processing input: {e}" #"Sorry, I'm having trouble processing your request."


    def execute_task(self, task):
        # Placeholder for task execution logic
        print(f"{self.name} is executing task: {task}")
        # Here you would implement the logic to process the task and generate a response using the OpenAI API
        response = self.client.chat.completions.create(
            model="gpt-4",
            messages=[
                {"role": "system", "content": self.description},
                {"role": "user", "content": task}
            ]
        )
        return response.choices[0].message.content