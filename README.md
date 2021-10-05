# BranchReloadMain


This project is meant to be launched by Visual Studio or Rider as a build configuration, usually when the user presses F5.
The program will change to the specified repository and branch provided in the JSON file specified, then push the contents of the working directory to that branch.
When completing the push, a github action will be triggered via webrequest as a repository-dispatch event.

These are the initial CI/CD steps for the rest of the system that is being worked on.
The github action that this program triggers is meant to communicate with a service listening for these events.

The practical use would be:

1) Build a program on your local development pc.

2) F5 to build the project

3) The directory is pushed to the target branch and the action alerts the management service.

4) The management service alerts the generator clusters that build from the branch provided.

5) The program is containerized and launched, allowing the developer to observe the changes on the live web.
