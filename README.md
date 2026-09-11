# Description

Multiple of my recent projects have required the usage of IK. While the inverse kinematics currently present in Godot are very impressive I found that they either over complicated or did not correctly work for my needs. As a result I implemented a Fabrik IK solver myself that I could more easily fine tune.

As a part of that process this repository also contains a few debug/visual aid tools including a skeleton debug draw tool.

# Current Progress

## Fabrik IK solver - Complete
The Fabrik IK solver is feature finished as of right now, including a pole/magnet system for knees/elbows.
<img width="640" height="465" alt="legshowcase" src="https://github.com/user-attachments/assets/0b637416-c99d-447e-84dc-71022798c4e5" />

## Quad-Spider Movement - In Progress
As of this commit the quad spider is able to move and rotate with 4 legs that take turns moving with a very simple gait system. The spider automatically adjusts its height to the average height of its 4 legs. Feet are moved to follow hip targets with an added Sin wave to the y position of the foot to force the spider to lift and place back down its legs.

Next steps will be a more complex gait system, allowing legs to move as long as one of its neighbors are on the ground. Similar to how actual spiders move. 
### Spider Basic Movement
https://github.com/user-attachments/assets/b98da2d8-5d16-4ae3-b0ce-dd81dad09ea0

### Spider Basic Rotation
https://github.com/user-attachments/assets/ef2833b3-4bd2-4334-a588-dce0369d30ce

## Fish Movement - In Progress
As of this commit fish spines can be generated based on a list of bone lengths. All aspects of fish IK work in editor, they will move towards the target Node. IK creatures with larger amounts of small bones better resemble serpents and will be used in later development for that purpose.

Next steps will be a more complex spine movement system, applying a sin wave to simulate how fix naturally move.

### Fish Basic Movement
https://github.com/user-attachments/assets/6e8d29e0-c3d2-4848-8003-efac0173e3ae

### Sea Snake Basic Movement
https://github.com/user-attachments/assets/972ff9a1-e5b7-4cf9-bf7e-e7908032b7cc

## Snake Movement - Very In Progress
As of this commit there is now an optional mode in the fish spine IK to enable a grounding mode, forcing a percentage of the IK creatures bones to be grounded at a time. This forces snake like movement to some degree. This mode is the most in progress as it currently has issues with bones clipping and odd head rotation. 

Future development will focus on resolving these base issues and then will work on applying the sin wave to both fish and snakes. 

### Grounded Snake Basic Movement]
https://github.com/user-attachments/assets/488b6726-11c8-463d-ad85-a6aaee8b0eaa


## Humanoid Movement - In Progress
As of this commit humanoid walk cycles are now working. It uses a similar leg Ik as the spider but rotates the target points to simulate the location of the feet during a walk cycle. The speed of the humanoid is synced to the rotational speed of their feet target positions.

The arms of the humanoids use the same Fabrik IK solvers as the legs but use a much more complicated pole system so that no matter the rotation the elbows are facing the correct way. This complex pole variant could easily be used for the legs in all modes but would likely be an over-complication for them.

The head of the humanoids use a very simple look at IK structure which limits the amount the head/neck can turn to look at the target.  

Next steps will be fine tuning the movement further.

### Human Walk Cycle
https://github.com/user-attachments/assets/c644e8ab-9b24-4f9b-b112-4b842ef6800b

### Human Basic Movement
https://github.com/user-attachments/assets/80e213a3-ece5-44b7-9ce0-64f4ae96a3fb

### Human Arm Basic Movement
https://github.com/user-attachments/assets/835a204b-6dd1-44c6-b585-83a4ef1039c5

### Human Head Basic Movement
https://github.com/user-attachments/assets/2db49cdf-9845-4d92-8fee-b981e0153196











