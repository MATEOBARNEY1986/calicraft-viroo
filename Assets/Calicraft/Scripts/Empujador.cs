using UnityEngine;

// Va en el jugador: al chocar con algo que tiene física (TNT, escombros) lo empuja.
// (en Godot era el apply_central_impulse de personaje.gd)
public class Empujador : MonoBehaviour
{
    void OnControllerColliderHit(ControllerColliderHit h)
    {
        var rb = h.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic || h.moveDirection.y < -0.3f) return;
        rb.AddForce(new Vector3(h.moveDirection.x, 0, h.moveDirection.z) * 1.5f, ForceMode.Impulse);
    }
}
