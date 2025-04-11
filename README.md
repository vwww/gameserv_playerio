# Game Servers on PlayerIO
This project is the code for my game server backends hosted on PlayerIO.

## Planned Refactor
This project has been quickly hacked together to support the game clients, which were also hacked together.

In the future, when time permits, this project should be refactored:
- ~~serialize and use byte array messages instead of depending on PlayerIO-specific serialization~~
- clean up the code in general
- remove "player number" (`playerInfo` index) and only use client number (add nullable `playerInfo` in or move its members into `Player` class)
- rename uppercase variable names copied verbatim while migrating the Go server code
