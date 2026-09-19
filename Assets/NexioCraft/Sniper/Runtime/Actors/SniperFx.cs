using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NexioCraft.Sniper
{
    /// <summary>
    /// Pooled visual effects for a session: sparks, smoke, dust, flashes, bullet holes, tracers and the bullet
    /// itself for the bullet cam. Particle systems are created once and fed with Emit, so effects cost no allocations.
    /// </summary>
    public sealed class SniperFx : MonoBehaviour
    {
        const int MaxHoles = 24;

        ParticleSystem sparks, smoke, dust, flash;
        readonly Queue<GameObject> holes = new Queue<GameObject>();
        Material holeMaterial;
        Mesh quad;
        Mesh bulletMesh;

        public static SniperFx Create(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<SniperFx>();
            fx.Setup();
            return fx;
        }

        void Setup()
        {
            var sparkMaterial = new Material(SniperMaterials.Additive) { mainTexture = SniperMaterials.Spark };
            sparks = System("Sparks", sparkMaterial, true, 1.1f, Fade(1f, 1f, 0f));
            smoke = System("Smoke", SniperMaterials.Blended, false, -0.04f, Fade(0.9f, 0.6f, 0f), Grow(0.45f, 1.8f));
            dust = System("Dust", SniperMaterials.Blended, false, 0.25f, Fade(0.9f, 0.5f, 0f), Grow(0.4f, 1.6f));
            flash = System("Flash", SniperMaterials.Additive, false, 0f, Fade(1f, 0.4f, 0f), Grow(0.6f, 1.4f));
            holeMaterial = new Material(SniperMaterials.Decal) { name = "Bullet Hole", mainTexture = SniperMaterials.HoleRing };
            quad = BuildQuad();
        }

        static Gradient Fade(float start, float middle, float end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(start, 0f), new GradientAlphaKey(middle, 0.4f), new GradientAlphaKey(end, 1f) });
            return gradient;
        }

        static AnimationCurve Grow(float from, float to) => AnimationCurve.EaseInOut(0f, from, 1f, to);

        ParticleSystem System(string name, Material material, bool stretch, float gravity, Gradient colour, AnimationCurve size = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startLifetime = 1f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var colourOverLife = system.colorOverLifetime;
            colourOverLife.enabled = true;
            colourOverLife.color = new ParticleSystem.MinMaxGradient(colour);

            if (size != null)
            {
                var sizeOverLife = system.sizeOverLifetime;
                sizeOverLife.enabled = true;
                sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, size);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.035f;
                renderer.lengthScale = 1.5f;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
            system.Play();
            return system;
        }

        public void Sparks(Vector3 point, Vector3 normal, Color colour, int count, float speed = 9f)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                var direction = (normal.normalized + Random.insideUnitSphere * 0.95f).normalized;
                emit.position = point;
                emit.velocity = direction * Random.Range(speed * 0.3f, speed);
                emit.startSize = Random.Range(0.035f, 0.08f);
                emit.startLifetime = Random.Range(0.25f, 0.7f);
                emit.startColor = Color.Lerp(colour, Color.white, Random.Range(0f, 0.5f));
                sparks.Emit(emit, 1);
            }
        }

        public void Smoke(Vector3 point, int count, Color colour, float size = 0.9f)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                emit.position = point + Random.insideUnitSphere * 0.3f;
                emit.velocity = Vector3.up * Random.Range(0.4f, 1.3f) + Random.insideUnitSphere * 0.5f;
                emit.startSize = size * Random.Range(0.7f, 1.3f);
                emit.startLifetime = Random.Range(1.6f, 3f);
                emit.startColor = colour;
                emit.rotation = Random.Range(0f, 360f);
                smoke.Emit(emit, 1);
            }
        }

        public void Dust(Vector3 point, Vector3 normal, Color colour)
        {
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < 9; i++)
            {
                emit.position = point + normal * 0.05f;
                emit.velocity = (normal + Random.insideUnitSphere * 0.7f) * Random.Range(0.8f, 2.4f);
                emit.startSize = Random.Range(0.25f, 0.55f);
                emit.startLifetime = Random.Range(0.6f, 1.1f);
                emit.startColor = colour;
                dust.Emit(emit, 1);
            }
        }

        public void Flash(Vector3 point, float size, Color colour)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = point,
                startSize = size,
                startLifetime = 0.16f,
                startColor = colour
            };
            flash.Emit(emit, 1);
        }

        public void Explosion(Vector3 point)
        {
            Flash(point + Vector3.up * 0.6f, 7f, new Color(1f, 0.8f, 0.45f));
            Flash(point + Vector3.up * 1.2f, 4.5f, new Color(1f, 0.55f, 0.2f));
            Sparks(point + Vector3.up * 0.5f, Vector3.up, new Color(1f, 0.6f, 0.2f), 70, 18f);
            Smoke(point + Vector3.up * 1f, 22, new Color(0.18f, 0.17f, 0.17f, 0.85f), 2.4f);
        }

        public void BulletHole(Vector3 point, Vector3 normal)
        {
            GameObject hole;
            if (holes.Count >= MaxHoles)
            {
                hole = holes.Dequeue();
            }
            else
            {
                hole = new GameObject("Hole");
                hole.transform.SetParent(transform, false);
                hole.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = hole.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = holeMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            hole.transform.position = point + normal * 0.02f;
            hole.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            hole.transform.localScale = Vector3.one * Random.Range(0.16f, 0.24f);
            holes.Enqueue(hole);
        }

        /// <summary>A faint streak along the bullet's path, so a miss shows where the shot went.</summary>
        public void Tracer(List<Vector3> path)
        {
            if (path == null || path.Count < 3) return;
            StartCoroutine(TracerRoutine(new List<Vector3>(path)));
        }

        IEnumerator TracerRoutine(List<Vector3> path)
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = SniperMaterials.Additive;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 2;

            // Skip the first metres (they would smear across the scope) and thin the points out.
            var points = new List<Vector3>();
            float travelled = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                travelled += Vector3.Distance(path[i - 1], path[i]);
                if (travelled < 14f) continue;
                if (points.Count == 0 || i % 6 == 0 || i == path.Count - 1) points.Add(path[i]);
            }
            if (points.Count < 2)
            {
                Destroy(go);
                yield break;
            }
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.widthCurve = AnimationCurve.Linear(0f, 0.04f, 1f, 0.16f);

            const float life = 0.35f;
            float age = 0f;
            while (age < life)
            {
                float alpha = 0.55f * (1f - age / life);
                var start = new Color(1f, 0.92f, 0.7f, alpha * 0.4f);
                var end = new Color(1f, 0.95f, 0.8f, alpha);
                line.startColor = start;
                line.endColor = end;
                age += Time.unscaledDeltaTime;
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>A brass bullet with a glowing trail, for the bullet cam.</summary>
        public Transform CreateBullet()
        {
            if (bulletMesh == null)
            {
                var mb = new MeshBuilder();
                var brass = new Color(0.86f, 0.64f, 0.3f);
                mb.Matrix = Matrix4x4.Rotate(Quaternion.Euler(90f, 0f, 0f));
                mb.Cylinder(new Vector3(0f, -0.03f, 0f), 0.0078f, 0.036f, 10, MeshBuilder.Tone(brass), MeshBuilder.Tone(brass));
                mb.Cone(new Vector3(0f, 0.006f, 0f), 0.0078f, 0.024f, 10, MeshBuilder.Tone(new Color(0.72f, 0.45f, 0.25f)), MeshBuilder.Tone(brass));
                bulletMesh = mb.ToMesh("Bullet");
            }
            var go = new GameObject("Bullet");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 2.2f;
            go.AddComponent<MeshFilter>().sharedMesh = bulletMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SniperMaterials.Lit;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = SniperMaterials.Additive;
            trail.time = 0.06f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.035f, 1f, 0f);
            trail.startColor = new Color(1f, 0.95f, 0.85f, 0.5f);
            trail.endColor = new Color(1f, 0.9f, 0.7f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Fx Quad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>A red barrel that explodes when shot, taking out anything close by.</summary>
    public sealed class ExplosiveBarrel : MonoBehaviour
    {
        public const float BlastRadius = 6.5f;

        static Mesh mesh;

        public bool Exploded { get; private set; }

        public static ExplosiveBarrel Create(Vector3 position, Transform parent)
        {
            if (mesh == null)
            {
                var mb = new MeshBuilder();
                var red = new Color(0.82f, 0.16f, 0.14f);
                var dark = new Color(0.35f, 0.08f, 0.07f);
                var hazard = new Color(1f, 0.8f, 0.15f);
                mb.Cylinder(Vector3.zero, 0.3f, 0.92f, 12, MeshBuilder.Tone(red), MeshBuilder.Tone(new Color(0.55f, 0.55f, 0.58f)));
                mb.Cylinder(new Vector3(0f, 0.2f, 0f), 0.31f, 0.05f, 12, MeshBuilder.Tone(dark), MeshBuilder.Tone(dark), false);
                mb.Cylinder(new Vector3(0f, 0.68f, 0f), 0.31f, 0.05f, 12, MeshBuilder.Tone(dark), MeshBuilder.Tone(dark), false);
                mb.Cylinder(new Vector3(0f, 0.4f, 0f), 0.305f, 0.12f, 12, MeshBuilder.Tone(hazard, 0.15f), MeshBuilder.Tone(hazard), false);
                mesh = mb.ToMesh("Barrel");
            }
            var go = new GameObject("Barrel");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SniperMaterials.Lit;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.46f, 0f);
            box.size = new Vector3(0.62f, 0.92f, 0.62f);
            return go.AddComponent<ExplosiveBarrel>();
        }

        public void Explode()
        {
            if (Exploded) return;
            Exploded = true;
            gameObject.SetActive(false);
        }
    }
}
