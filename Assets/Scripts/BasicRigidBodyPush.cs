using UnityEngine;
using StarterAssets;
using System.Collections.Generic;

public class BasicRigidBodyPush : MonoBehaviour
{
    public LayerMask pushLayers;
    public bool canPush;
    [Range(0.5f, 5f)] public float strength = 1.1f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Debug.Log("== OnControllerColliderHit with : " + hit.gameObject.tag);
        if (canPush) PushRigidBodies(hit);
		/* If not police, dont check collide */
        if (this.gameObject.tag != Constants.TAG_POLICE) return;

		/* If you are Police, let's check what you touch */
        if (hit.gameObject.tag == Constants.TAG_THIEF)
        {
            /* Touched to Thief , let's do something */
			NetCodeThirdPersonController target = hit.gameObject.GetComponent<NetCodeThirdPersonController>();
			// Debug.Log("Touch to Thief : IsImmortal : " + target.IsImmortal.ToString());
			/* Firstly check if this thief is in immortal state -> do nothing
			If are playing as normal, trigger event that police touch this thief and do some logic */
			if(target.IsImmortal == false){
				/* Call func ON Touch Thief. */
				this.gameObject.GetComponent<NetCodeThirdPersonController>().OnTouchThief(target);
				
			}
			
        }
        else if (hit.gameObject.tag == Constants.TAG_POLICE)
        {
            Debug.Log("Touch to Police");
        }
    }

    private void PushRigidBodies(ControllerColliderHit hit)
    {
        // https://docs.unity3d.com/ScriptReference/CharacterController.OnControllerColliderHit.html

        // make sure we hit a non kinematic rigidbody
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;

        // make sure we only push desired layer(s)
        var bodyLayerMask = 1 << body.gameObject.layer;
        if ((bodyLayerMask & pushLayers.value) == 0) return;

        // We dont want to push objects below us
        if (hit.moveDirection.y < -0.3f) return;

        // Calculate push direction from move direction, horizontal motion only
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0.0f, hit.moveDirection.z);

        // Apply the push and take strength into account
        body.AddForce(pushDir * strength, ForceMode.Impulse);
    }
}