using UnityEngine;
using System.Collections.Generic;

public class AerodynamicSimulation : MonoBehaviour
{
    List<Particle> particles = new List<Particle>();
    public GameObject sphere;
    public int particlesCount = 100; 
    public float particleRadius = 0.2f;
    public float particleMass = 0.00125f;
    public float spawnRadius = 5f;
    public Vector3 gravityDirection = Vector3.down;
    public float windSpeed = 4f;
    private float timeCounter = 0f;

    public float lift = 1.0f;
    public float drag = 0.47f; 
    public float airDensity = 1.225f;  
    public Vector3 volumeSize = new Vector3(30, 30, 30);
    private MeshSplitter meshSplitter;
    private List<Triangle> triangles;
    private SpatialHash spatialHash;
    

    void Start()
    {
        InitializeParticles();
        meshSplitter = GetComponent<MeshSplitter>();
        if (meshSplitter != null)
        {
            triangles = MeshSplitter.triangles;
        }

        spatialHash = new SpatialHash(2 * particleRadius, particlesCount);
        UpdateSpatialHash();
    }

    void Update()
    {
        timeCounter += Time.deltaTime;
        
        foreach (Particle particle in particles)
        {
            Vector3 oldPosition = particle.position;
            CalculateForces(particle);
            ApplyGravity(particle);
            spatialHash.Query(particle.position, 2 * particleRadius);
            float cellSize = 4 * particleRadius;
            spatialHash = new SpatialHash(cellSize, particlesCount);
        }

        foreach (var particle in particles)
        {
            BoundaryCollisions(particle);
        }

        ResolveParticleCollisions();
        ResolveParticleTriangleCollisions();
        UpdateSpatialHash();
    }

    void InitializeParticles()
    {
        int maxAttempts = 1000; 
        
        for (int i = 0; i < particlesCount; i++)
        {
            Vector3 randomVector;
            bool validPosition;
            int attempts = 0;

            do
            {
                validPosition = true;
                randomVector = new Vector3(
                    UnityEngine.Random.Range(-spawnRadius, spawnRadius),
                    UnityEngine.Random.Range(-spawnRadius, spawnRadius),
                    UnityEngine.Random.Range(-spawnRadius, spawnRadius)
                );

                foreach (Particle existingParticle in particles)
                {
                    if (Vector3.Distance(randomVector, existingParticle.position) < 2 * particleRadius)
                    {
                        validPosition = false;
                        break;
                    }
                }

                attempts++;
                if (attempts >= maxAttempts)
                {
                    Debug.LogError("Could not find a valid position for new particle.");
                    return;
                }
            } while (!validPosition);

            GameObject oneParticle = Instantiate(sphere, randomVector, Quaternion.identity);
            Particle newParticle = oneParticle.AddComponent<Particle>();
            newParticle.position = randomVector;
            newParticle.velocity = Vector3.zero;
            newParticle.sphere = oneParticle;
            particles.Add(newParticle);
        }
    }

    void CalculateForces(Particle particle)
    {
        Vector3 relativeVelocity = particle.velocity - new Vector3(windSpeed, 0, 0);
        float speed = relativeVelocity.magnitude;

        Vector3 dragForce = -0.5f * airDensity * speed * speed * drag * Mathf.PI * Mathf.Pow(particleRadius, 2) * relativeVelocity.normalized;

        Vector3 liftDirection = Vector3.Cross(relativeVelocity.normalized, Vector3.up).normalized;
        Vector3 liftForce = 0.5f * airDensity * speed * speed * lift * Mathf.PI * Mathf.Pow(particleRadius, 2) * liftDirection;
        
        Vector3 totalForce = dragForce + liftForce;
        
        float damping = 0.99f;
        particle.velocity = particle.velocity * damping + totalForce / particleMass * Time.deltaTime;
        particle.position += particle.velocity * Time.deltaTime;
        particle.sphere.transform.position = particle.position;
    }

    void ApplyGravity(Particle particle)
    {
        float scaledGravity = 0.1f;
        particle.velocity += gravityDirection * scaledGravity * Time.deltaTime;
        particle.position += particle.velocity * Time.deltaTime;
        particle.sphere.transform.position = particle.position;
    }

    void BoundaryCollisions(Particle particle)
    {
        Vector3 minBounds = -volumeSize / 2;
        Vector3 maxBounds = volumeSize / 2;
        float dampingFactor = 0.8f;
        
        for (int i = 0; i < 3; i++)
        {
            if (particle.position[i] < minBounds[i])
            {
                particle.position[i] = minBounds[i] + 0.01f;
                particle.velocity[i] *= -dampingFactor;
            }
            else if (particle.position[i] > maxBounds[i])
            {
                particle.position[i] = maxBounds[i] - 0.01f;
                particle.velocity[i] *= -dampingFactor;
            }
        }
        particle.transform.position = particle.position;
    }

    void ResolveParticleCollisions()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            spatialHash.Query(particles[i].position, 2 * particleRadius);

            for (int j = 0; j < spatialHash.QuerySize; j++)
            {
                int neighborIndex = spatialHash.QueryIds[j];
                if (neighborIndex == i) continue;

                ResolveCollisionForParticles(particles[i], particles[neighborIndex]);
            }
        }
    }

    void ResolveCollisionForParticles(Particle p1, Particle p2)
    {
        Vector3 delta = p1.position - p2.position;
        float distance = delta.magnitude;
        float minDistance = 2 * particleRadius;

        if (distance < minDistance)
        {
            Vector3 normal = delta.normalized;
            float penetrationDepth = minDistance - distance;
            Vector3 correction = normal * (penetrationDepth / 2);

            p1.position += correction;
            p2.position -= correction;

            float restitution = 0.8f;

            Vector3 relativeVelocity = p1.velocity - p2.velocity;
            float velocityAlongNormal = Vector3.Dot(relativeVelocity, normal);

            if (velocityAlongNormal > 0) return;

            float j = -(1 + restitution) * velocityAlongNormal / 2;
            Vector3 impulse = j * normal;

            p1.velocity += impulse;
            p2.velocity -= impulse;
        }
    }
    void ResolveParticleTriangleCollisions()
    {
        foreach (Triangle triangle in triangles)
        {
            foreach (Particle particle in particles)
            {
                Vector3 closestPoint = ClosestPointOnTriangle(particle.position, triangle);

                float distance = Vector3.Distance(particle.position, closestPoint);
                if (distance < particleRadius)
                {
                    Vector3 collisionNormal = (particle.position - closestPoint).normalized;
                    float penetrationDepth = particleRadius - distance;

                    // Move the particle out of the collision
                    particle.position += collisionNormal * penetrationDepth;

                    // Reflect the velocity based on the collision normal
                    particle.velocity = Vector3.Reflect(particle.velocity, collisionNormal);
                }
            }
        }
    }

    Vector3 ClosestPointOnTriangle(Vector3 point, Triangle triangle)
    {
        Vector3 edge0 = triangle.v1 - triangle.v0;
        Vector3 edge1 = triangle.v2 - triangle.v0;
        Vector3 v0ToPoint = point - triangle.v0;

        float a = Vector3.Dot(edge0, edge0);
        float b = Vector3.Dot(edge0, edge1);
        float c = Vector3.Dot(edge1, edge1);
        float d = Vector3.Dot(edge0, v0ToPoint);
        float e = Vector3.Dot(edge1, v0ToPoint);

        float det = a * c - b * b;
        float s = b * e - c * d;
        float t = b * d - a * e;

        if (s + t <= det)
        {
            if (s < 0)
            {
                if (t < 0)
                {
                    if (d < 0)
                    {
                        s = Mathf.Clamp01(-d / a);
                        t = 0;
                    }
                    else
                    {
                        s = 0;
                        t = Mathf.Clamp01(-e / c);
                    }
                }
                else
                {
                    s = 0;
                    t = Mathf.Clamp01(-e / c);
                }
            }
            else if (t < 0)
            {
                s = Mathf.Clamp01(-d / a);
                t = 0;
            }
            else
            {
                s /= det;
                t /= det;
            }
        }
        else
        {
            if (s < 0)
            {
                float tmp0 = b + d;
                float tmp1 = c + e;
                if (tmp1 > tmp0)
                {
                    float numer = tmp1 - tmp0;
                    float denom = a - 2 * b + c;
                    s = Mathf.Clamp01(numer / denom);
                    t = 1 - s;
                }
                else
                {
                    t = Mathf.Clamp01(-e / c);
                    s = 0;
                }
            }
            else if (t < 0)
            {
                if (a + d > b + e)
                {
                    float numer = c + e - b - d;
                    float denom = a - 2 * b + c;
                    s = Mathf.Clamp01(numer / denom);
                    t = 1 - s;
                }
                else
                {
                    s = Mathf.Clamp01(-d / a);
                    t = 0;
                }
            }
            else
            {
                float numer = c + e - b - d;
                float denom = a - 2 * b + c;
                s = Mathf.Clamp01(numer / denom);
                t = 1 - s;
            }
        }

        return triangle.v0 + edge0 * s + edge1 * t;
    }

    void UpdateSpatialHash()
    {
        Vector3[] positions = new Vector3[particles.Count];
        for (int i = 0; i < particles.Count; i++)
        {
            positions[i] = particles[i].position;
        }
        spatialHash.Create(positions);
    }
}
