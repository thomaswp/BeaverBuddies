
# Movement

Walker: Just calls PathFollower.MoveAlongPath

PathFollower: contains the current long-term path, e.g. from one building to the next and determines the movement that should occur for each tick. Doesn't save state. 
* Calls MovementAnimator.AnimateMovementAlongPath with a set of PathCorners, with a position and game time.
* The positions and deltas seem to be derministic, given the same start state. However, since the PathCorners use an actualy Time, instead of a delta time, rounding errors seem to acrue.
* Interestingly, it keeps it's own internal transform that is updated via deltaFixedTime, so it does not use the position of the CharacterModel to determine the next position. However, other game logic may do so.

MovementAnimator: An Update-ing component that updates the movement and rotation of the CharacterModel each frame (rather than each tick).
* Saves the Position and LeftTimeInSeconds of the current animation. The latter is the time left until the short-term animation completes (usually ~1s + 1 frame, since animations always add a second at the end). These are the values that tend to get desynced.
* The position it uses and saves is actually stored in the AnimatedPathFollower.
* The position the model is updated to is also stored in the AnimatedPathFollower.
* When it updates, it updates the AnimatedPathFollower, then updates the CharacterModel's Position and Rotation

AnimatedPathFollower: Does the actual math to determine, for a given frame, where the CharacterModel should end up. It doesn't save or update any of these values. Just does the math.
* It is updated by MovementAnimator.
* It keeps track of a list of PathCorners, given to it via the MovementAnimator by the PathFollower.
* This is likely where the actual desync occurs, since it uses the actual time, rather than a delta time, to determine the position of the CharacterModel.
* It's update increments _nextCornerIndex to the last corner that is past the "current" time. If all corners are past the current time, it sets the index to 1 + the length to indicate out of bounds.
* PlaceAtCorrectPosition then interpolates between the current corner and the next corner, based on the current time and the time of the next corner.
* To make this work, MovementAnimator always adds a duplicate of the last corner at +1 seconds, so that if the time goes a little past the end of the tick, it should still be able to interpolate to the correct position.
* What I don't understand is why telling it to animate to past the end of the Tick doesn't always bring it to a deterministic end of the 

CharacterModel:
* Has a refernce to the GameObject's transform (`_model`) - at leas I think, so this is how it actually gets updated.
* Saves the rotation of the character and stores (but doesn't save) the position of the character (this is saved by the MovementAnimator).