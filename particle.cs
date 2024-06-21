using UnityEngine;
[System.Serializable]
public class Particle : MonoBehaviour
{
    public Vector3 position;
    public Vector3 velocity;
    public float mass;
    public float density;
    public float pressure;
    public GameObject sphere;
    

    public void Initialize(Vector3 startPosition, Vector3 startVelocity, float particleMass)
    {
        position = startPosition;
        velocity = startVelocity;
        mass = particleMass;

        transform.position = position;
    }
}
