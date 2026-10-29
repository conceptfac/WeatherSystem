using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ConceptFactory.Weather.Atmosphere
{
    /// <summary>
    /// Wind source for foliage shaders (Unity's own WindZone does not reach custom shaders).
    /// The wind blows along this object's forward direction, projected on the ground plane.
    /// It knows nothing about who reads it: it only uploads the _WindZone* shader globals, by itself, every frame.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class WindZone : MonoBehaviour
    {
        public const int MaxZones = 8;

        public enum Mode
        {
            [Tooltip("Affects all the grass, everywhere.")] Global,
            [Tooltip("Affects grass around this object and fades out toward the radius.")] Local,
        }

        public Mode mode = Mode.Global;
        public enum Blend { Override, Additive }
        public enum Shape { Radius, Cone, Cube }

        [Tooltip("Override replaces global wind inside this zone, fading at its edges. Overlapping overrides blend together. Additive adds a separate current on top.")]
        public Blend blend = Blend.Override;
        public Shape shape = Shape.Radius;
        [Tooltip("Radius of the cone's entry ring, in meters. Zero gives a pointed cone.")]
        [Min(0f)] public float coneStartRadius = 0.25f;
        [Tooltip("Radius of the cone's exit ring, in meters.")]
        [Min(0.01f)] public float coneEndRadius = 2f;
        [Tooltip("Distance between the entry and exit rings along this object's forward axis, in meters.")]
        [Min(0.1f)] public float coneRange = 5f;
        [Tooltip("Fraction of the cone's length used to fade out at its exit.")]
        [Range(0.01f, 1f)] public float coneEndFade = 0.2f;
        [Tooltip("Local mode only: distance where the wind has faded to nothing.")]
        [Min(0.1f)] public float radius = 20f;
        [Tooltip("Local mode only: fraction of the radius with full strength.")]
        [Range(0f, 1f)] public float innerFraction = 0.4f;
        [Tooltip("Local Cube dimensions in meters (X, Y, Z).")]
        public Vector3 cubeScale = Vector3.one;

        [Header("Wind")]
        [Tooltip("How far the blade tips lean, in meters.")]
        [Min(0f)] public float strength = 0.04f;
        [Tooltip("How fast gusts travel across the field, in meters per second.")]
        [Min(0f)] public float speed = 1.5f;
        [Tooltip("Distance between gusts, in meters.")]
        [Min(0.5f)] public float wavelength = 6f;
        [Tooltip("0 = steady, 1 = gusts vary a lot across the field.")]
        [Range(0f, 1f)] public float turbulence = 0.4f;

        static readonly List<WindZone> Zones = new List<WindZone>();
        static readonly Vector4[] ZoneA = new Vector4[MaxZones];
        static readonly Vector4[] ZoneB = new Vector4[MaxZones];
        static readonly Vector4[] ZoneC = new Vector4[MaxZones];
        static readonly Vector4[] ZoneD = new Vector4[MaxZones];
        static readonly Vector4[] ZoneE = new Vector4[MaxZones];

        static readonly int ZoneAId = Shader.PropertyToID("_WindZoneA");
        static readonly int ZoneBId = Shader.PropertyToID("_WindZoneB");
        static readonly int ZoneCId = Shader.PropertyToID("_WindZoneC");
        static readonly int ZoneDId = Shader.PropertyToID("_WindZoneD");
        static readonly int ZoneEId = Shader.PropertyToID("_WindZoneE");
        static readonly int ZoneCountId = Shader.PropertyToID("_WindZoneCount");

        void OnEnable()
        {
            Zones.Add(this);
            UploadAll();
        }

        void OnDisable()
        {
            Zones.Remove(this);
            UploadAll();
        }

        // One zone sends everything, once per frame, so nothing else has to know the wind exists.
        void LateUpdate()
        {
            if (Zones.Count > 0 && Zones[0] == this) UploadAll();
        }

#if UNITY_EDITOR
        // Edit mode has no LateUpdate every frame: keep the globals fresh so painting and tweaking show the wind.
        [InitializeOnLoadMethod]
        static void HookEditor()
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }

        static void EditorTick()
        {
            if (!Application.isPlaying && Zones.Count > 0) UploadAll();
        }
#endif

        /// <summary>Sends the active zones to the shaders that read the _WindZone* globals.</summary>
        public static void UploadAll()
        {
            int count = Mathf.Min(Zones.Count, MaxZones);
            for (int i = 0; i < count; i++)
            {
                var z = Zones[i];
                Vector3 pos = z.transform.position;
                Vector3 fwd = z.transform.forward;
                Vector2 dir = new Vector2(fwd.x, fwd.z);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;

                bool local = z.mode == Mode.Local;
                ZoneA[i] = new Vector4(pos.x, pos.y, pos.z, z.radius);
                ZoneB[i] = new Vector4(dir.x, dir.y, z.speed, z.strength);
                ZoneC[i] = new Vector4(z.turbulence, z.wavelength, local ? 1f : 0f, z.innerFraction);
                ZoneD[i] = new Vector4(z.blend == Blend.Override ? 1f : 0f,
                    z.shape == Shape.Cone ? 1f : z.shape == Shape.Cube ? 2f : 0f,
                    z.shape == Shape.Cube ? Mathf.Abs(z.cubeScale.y) * 0.5f : z.coneStartRadius,
                    z.shape == Shape.Cube ? Mathf.Abs(z.cubeScale.x) * 0.5f : z.coneEndRadius);
                ZoneE[i] = new Vector4(fwd.x, fwd.y, fwd.z,
                    z.shape == Shape.Cube ? Mathf.Abs(z.cubeScale.z) * 0.5f : z.coneRange);
                // For cones the radius slot carries the axial fade fraction instead.
                if (local && z.shape == Shape.Cone) ZoneA[i].w = z.coneEndFade;
            }

            Shader.SetGlobalVectorArray(ZoneAId, ZoneA);
            Shader.SetGlobalVectorArray(ZoneBId, ZoneB);
            Shader.SetGlobalVectorArray(ZoneCId, ZoneC);
            Shader.SetGlobalVectorArray(ZoneDId, ZoneD);
            Shader.SetGlobalVectorArray(ZoneEId, ZoneE);
            Shader.SetGlobalFloat(ZoneCountId, count);
        }

        void OnValidate()
        {
            UploadAll();
            radius = Mathf.Max(0.1f, radius);
            innerFraction = Mathf.Clamp01(innerFraction);
            coneStartRadius = Mathf.Max(0f, coneStartRadius);
            coneEndRadius = Mathf.Max(0.01f, coneEndRadius);
            coneRange = Mathf.Max(0.1f, coneRange);
            coneEndFade = Mathf.Clamp(coneEndFade, 0.01f, 1f);
            cubeScale.x = Mathf.Max(0.01f, Mathf.Abs(cubeScale.x));
            cubeScale.y = Mathf.Max(0.01f, Mathf.Abs(cubeScale.y));
            cubeScale.z = Mathf.Max(0.01f, Mathf.Abs(cubeScale.z));
        }

        void OnDrawGizmos()
        {
            Vector3 p = transform.position;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;

            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.95f);
            float length = Mathf.Max(2f, Mathf.Lerp(2f, 6f, speed / 10f));
            Vector3 tip = p + fwd * length;
            Gizmos.DrawLine(p, tip);

            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Gizmos.DrawLine(tip, tip - fwd * 0.6f + side * 0.35f);
            Gizmos.DrawLine(tip, tip - fwd * 0.6f - side * 0.35f);
        }

        void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (Selection.activeGameObject != gameObject) return;
#endif
            if (mode != Mode.Local) return;

            if (shape == Shape.Cone)
            {
                DrawCone(coneStartRadius, coneEndRadius, 0.9f);
                DrawCone(coneStartRadius * innerFraction, coneEndRadius * innerFraction, 0.4f);
                return;
            }

            if (shape == Shape.Cube)
            {
                Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.18f);
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, cubeScale);
                Gizmos.DrawCube(Vector3.zero, Vector3.one);
                Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
                return;
            }

            Vector3 p = transform.position;

            // Outer sphere: where the wind has faded out. Inner sphere: where it is at full strength.
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.12f);
            Gizmos.DrawSphere(p, radius);
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(p, radius);
        }

        void DrawCone(float startRadius, float endRadius, float alpha)
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, alpha);
            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * coneRange;
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 u = transform.right * Mathf.Cos(a) + transform.up * Mathf.Sin(a);
                Vector3 v = transform.right * Mathf.Cos(b) + transform.up * Mathf.Sin(b);
                Gizmos.DrawLine(start + u * startRadius, start + v * startRadius);
                Gizmos.DrawLine(end + u * endRadius, end + v * endRadius);
                if (i % 12 == 0) Gizmos.DrawLine(start + u * startRadius, end + u * endRadius);
            }
        }
    }
}
