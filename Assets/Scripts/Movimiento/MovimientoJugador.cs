using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovimientoJugador : MonoBehaviour
{
    public float velocidad = 5f;
    CharacterController cc;

    void Awake() => cc = GetComponent<CharacterController>();

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        cc.Move((dir * velocidad + Physics.gravity) * Time.deltaTime);
    }
}