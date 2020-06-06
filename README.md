# Alexa, Is This A Mood? An investigation into the effects voice user interfaces have on mood tracking.

## Introduction
Is This A Mood is a dissertation project for my final year of Computer Science at the University of Kent. This repository contains the system that was developed for the purposes of conducting research on Alexa's (and by proxy other voice assitants') ability to be used as a mood journal. You can read the final report [here](https://github.com/Brawrdon/IsThisAMood/blob/master/Alexa%20Is%20This%20A%20Mood%20-%20Technical%20Report.pdf).

## Project Structure
### /app
This folder contains a [Flutter](https://flutter.dev/) project. The original design of the study included the usage of a basic mood journaling app to generate control data, however, due to time constraints this was deprecated. The folder contains a (very) unfinished app.

### /config
Stores configurations used for the system.

### /designs
Contains [diagrams.net](https://www.diagrams.net/) UML and other design stage mockups.

### /server
Contains an ASP.NET Core backend used to host a custom Alexa skill.
