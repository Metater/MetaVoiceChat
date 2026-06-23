# Extras require the following packages
- Cinemachine (3.17 tested, but different may work)
- Input System (1.19.0 tested, but different may work)

# If you want the intended player collision functionality, do the following:
1. Create a "Player" layer
2. Assign Player game objects to "Player"
3. Set "MetaPlayer"/"Repulsion"/Meta Player Repulsion/Player Layer Mask to "Player"
4. Set "MetaPlayer"/Meta Player Config/Inverted Ground Layers to "Player" and any other layers you don't want the player to be able to jump off of
5. In "Project Settings"/"Physics Settings"/Layer Collision Matrix, disallow Player-Player collision, so the repulsion works properly
