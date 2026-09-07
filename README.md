# Description

Multiple of my recent projects have required the usage of IK. While the inverse kinematics currently present in Godot are very impressive I found that they either over complicated or did not correctly work for my needs. As a result I implemented a Fabrik IK solver myself that I could more easily fine tune.

As a part of that process this repository also contains a few debug/visual aid tools including a skeleton debug draw tool.

# Current Progress

## Fabrik IK solver - Complete
The Fabrik IK solver is feature finished as of right now, including a pole/magnet system for knees/elbows.
<img width="640" height="465" alt="legshowcase" src="https://github.com/user-attachments/assets/0b637416-c99d-447e-84dc-71022798c4e5" />

## Quad-Spider Movement - In Progress
As of this commit the quad spider is able to move and rotate with 4 legs that take turns moving with a very simple gait system. The spider automatically adjusts its height to the average height of its 4 legs. Feet are moved to follow hip targets with an added Sin wave to the y position of the foot to force the spider to lift and place back down its legs.

Next steps will be a more in depth controller as well as more fluid movement. 

# Spider Basic Movement
https://github.com/user-attachments/assets/b98da2d8-5d16-4ae3-b0ce-dd81dad09ea0

# Spider Basic Rotation
https://github.com/user-attachments/assets/ef2833b3-4bd2-4334-a588-dce0369d30ce


