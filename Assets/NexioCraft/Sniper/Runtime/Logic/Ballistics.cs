using System.Collections.Generic;
using UnityEngine;

namespace NexioCraft.Sniper
{
    /// <summary>How a rifle's bullet flies. Gravity and wind are exaggerated a little so range matters in play.</summary>
    public struct BallisticProfile
    {
        /// <summary>Metres per second as the bullet leaves the barrel.</summary>
        public float MuzzleSpeed;
        /// <summary>Downward acceleration, m/s².</summary>
        public float Gravity;
        /// <summary>Sideways acceleration per m/s of wind.</summary>
        public float WindFactor;
        /// <summary>The sight is zeroed here: the bullet crosses the line of sight at this distance.</summary>
        public float ZeroDistance;
        public float MaxRange;
    }

    /// <summary>Checks one straight piece of the bullet's path; returns true (and where along it) if something is hit.</summary>
    public delegate bool SegmentCast(Vector3 from, Vector3 to, out float fraction);

    /// <summary>Bullet flight: launch angle from the zeroing, a stepped trajectory, and holdover for the scope marks.</summary>
    public static class Ballistics
    {
        /// <summary>Simulation step for real shots (fine enough for a smooth bullet-cam path).</summary>
        public const float ShotStep = 1f / 240f;

        /// <summary>Coarser step for the per-frame impact preview; each segment is still ray-cast exactly.</summary>
        public const float PreviewStep = 1f / 60f;

        public static readonly BallisticProfile Rifle = new BallisticProfile
        {
            MuzzleSpeed = 600f,
            Gravity = 9.81f,
            WindFactor = 1.2f,
            ZeroDistance = 100f,
            MaxRange = 900f
        };

        /// <summary>How far the barrel points above the sight line (radians) so the shot meets it at the zero distance.</summary>
        public static float ZeroAngle(BallisticProfile p)
        {
            // Solve y(d) = 0 for a level shot: d·tanθ = g·d² / (2·v²·cos²θ). For these small angles
            // sin(2θ) = g·d / v² is exact for the drag-free model used here.
            float s = Mathf.Clamp(p.Gravity * p.ZeroDistance / (p.MuzzleSpeed * p.MuzzleSpeed), -1f, 1f);
            return 0.5f * Mathf.Asin(s);
        }

        /// <summary>Launch velocity for a shot whose sight line is <paramref name="aim"/>.</summary>
        public static Vector3 LaunchVelocity(Vector3 aim, BallisticProfile p)
        {
            aim.Normalize();
            // The upward direction perpendicular to the aim, in the vertical plane that contains it.
            var up = Vector3.up - aim * Vector3.Dot(Vector3.up, aim);
            if (up.sqrMagnitude < 1e-8f) return aim * p.MuzzleSpeed;
            up.Normalize();
            float angle = ZeroAngle(p);
            return (aim * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * p.MuzzleSpeed;
        }

        public static Vector3 Acceleration(Vector3 wind, BallisticProfile p) =>
            new Vector3(wind.x * p.WindFactor, -p.Gravity, wind.z * p.WindFactor);

        /// <summary>
        /// Flies a bullet until <paramref name="cast"/> reports a hit or it passes the maximum range.
        /// <paramref name="path"/> (optional) receives every point, ending at the impact.
        /// </summary>
        public static bool Trace(Vector3 origin, Vector3 velocity, Vector3 wind, BallisticProfile p, SegmentCast cast,
            List<Vector3> path, out Vector3 impact, out float flightTime, float step = ShotStep, float maxRange = -1f)
        {
            if (maxRange <= 0f) maxRange = p.MaxRange;
            path?.Clear();
            path?.Add(origin);
            var acceleration = Acceleration(wind, p);
            var position = origin;
            float time = 0f;
            float travelled = 0f;
            while (travelled < maxRange && time < 6f)
            {
                // Semi-implicit Euler: close to the exact parabola at these step sizes.
                velocity += acceleration * step;
                var next = position + velocity * step;
                if (cast != null && cast(position, next, out float fraction))
                {
                    impact = Vector3.Lerp(position, next, fraction);
                    flightTime = time + step * fraction;
                    path?.Add(impact);
                    return true;
                }
                travelled += (next - position).magnitude;
                position = next;
                time += step;
                path?.Add(position);
            }
            impact = position;
            flightTime = time;
            return false;
        }

        /// <summary>
        /// Height of the bullet above (+) or below (−) the sight line at <paramref name="distance"/> for a level
        /// shot, from the exact drag-free trajectory.
        /// </summary>
        public static float OffsetAt(float distance, BallisticProfile p)
        {
            float angle = ZeroAngle(p);
            float vx = p.MuzzleSpeed * Mathf.Cos(angle);
            float vy = p.MuzzleSpeed * Mathf.Sin(angle);
            float t = distance / vx;
            return vy * t - 0.5f * p.Gravity * t * t;
        }

        /// <summary>Angle (degrees) to aim above the target at this distance: where the scope's range marks sit.</summary>
        public static float HoldoverDegrees(float distance, BallisticProfile p) =>
            Mathf.Atan2(-OffsetAt(distance, p), distance) * Mathf.Rad2Deg;

        /// <summary>Sideways drift (metres, along the wind) at this distance for a crosswind of <paramref name="windSpeed"/>.</summary>
        public static float DriftAt(float distance, float windSpeed, BallisticProfile p)
        {
            float t = distance / p.MuzzleSpeed;
            return 0.5f * windSpeed * p.WindFactor * t * t;
        }
    }
}
