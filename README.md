# Portfolio for croozen
Portfolio of past and current work of Code Snippets and Content.

## Phone Number To List of Text Strings
This was an interview question years ago.  To write code that takes a phone number and finds all of the possible strings using the number's corresponding letters (For Example 2 => "a,b,c").  This example is to share C# Code as well work to do a recursive and non-recursive methods.
[Project Folder](https://github.com/croozen/Portfolio/tree/main/PhoneNumberToText)

## Activity Manager
Task Activity Manager that had a web interface for assigning out task to 1 or many individuals.  Or you could assign a task to a group that individuals would receive.  [Project Folder](https://github.com/croozen/Portfolio/tree/main/ActivityManager)
### SQL - Recurring activities
The SQL code snippets showcase how a recurrence task is created.  When a recuring task is created it creates virtual tasks which refer back to the original task.  This allows for changes to occur without managing all of the recurring task in the future.  Once a virtual task is updated, for example "it is marked completed" it hydrates a task so it can be tracked on its own.  
### Integration Test
The following is integration test, sharing this code makes me feel a bit vulnerable on code style - It was created 7 years ago to test a platform and I have done minor updates to it over the years to keep my engineering skills relevant.  This shows multiple things without actually sharing source implementation code of the product.  In addition this shows the importance of test automation, there were a handful of times where code changed and test failed due to an edge case not caught. I strongly believe Integration and Unit Tests are "Musts" for any solution/project. 
1. JWT Bearer Tokens - Authenticating and authorizing a user through tokens.
2. JSON Creation - There is both using Text String but also using JObject and JArray to populate a submission request
3. Voice Tasks, Audio File Dictation - Yes the application allowed a user to message via audio, it would use Azure Cognitive services to dictate the audio into text.  This required a call check if the task was completed before it tested successfully.

## Kusto Example from Kusto Detective Agency - Case 5
One of the things I have been wanting to do for a few years is to walkthrough some of the Kusto Detective Agency problems. If your not familiar with them they are [Kusto Detective](https://detective.kusto.io/).  These are fun exercises that start easy and quick to finish, by case 6 it is an Escape Room type excercise.  Key takeaways from the example.  The use of functions within Kusto to reduce code being reused.  The ability to join, filter, summarize of data.  Lastly taking minimal amount of requirements to find an elusive URL.  [Project Folder](https://github.com/croozen/Portfolio/tree/main/KustoDetective)
