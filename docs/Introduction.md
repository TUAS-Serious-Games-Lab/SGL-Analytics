# Introduction

The idea behind this system is that working scientifically with serious games can often benefit from capturing gameplay data directly from the games for later analysis to supplement a classical user survey-based evaluation.
Depending on what events and states are recorded from the studied game, there is a wide range of insights that could be gathered from the data, e.g.:
- Popularity of certain in-game activities across the player base or associated with demographic information gathered from the players (e.g. age-groups)
- Influence of certain collected factors on in-game performance (accuracy, time to solve a given task, ...)
- Differences in usage frequency
- Where in the game world certain actions happen especially frequently, e.g. trading items, chatting

To perform such analysis, one first needs to gather the raw data in a flexible and structured manner.
This system provides such a data gathering mechanism, consisting of a client-side library, backend services, and associated tools.

The client library can be used in games to record in-game events and states of selected important game objects.
To provide a simple labeling for later filtering, the events and states are recorded into logical channels within the log entry stream.

The backend services are intentionally split into two separate microservices to provide a clear separation between user registration data, which for some scenarios might need to include personally identifiable information, on one side and the actual log data on the other side.
In the standard deployment scenario they run on the same server but isolated at container-level, they can however also be deployed on two entirely different servers with a few adaptations in the deployment configuration and the client-side parameters.

The collected game logs are gatherd in the form of (compressed) JSON files which can then be post-processed with a bit of scripting to extract the currently relevant data in the desired format, e.g. CSV to work with it further.
