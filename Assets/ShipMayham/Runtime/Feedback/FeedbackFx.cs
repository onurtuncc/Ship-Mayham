using System.Collections.Generic;
using UnityEngine;

namespace ShipMayham
{
    public enum FxKind { Sparkle, Hearts, Anger, Confetti, Coins, Splash, Smoke, Stars, Foam, Flame }

    /// <summary>
    /// Pooled particle bursts and floating text popups. One ParticleSystem per kind lives under this object and is fired with
    /// Emit(position), so a burst costs no allocation. Textures are drawn procedurally; the material is serialized so its shader
    /// is guaranteed to be part of every build.
    /// </summary>
    public sealed class FeedbackFx : MonoBehaviour
    {
        private const int PopupPool = 16;

        public Material ParticleMaterial;
        public TextMesh PopupTemplate;
        public Camera ViewCamera;

        private readonly Dictionary<FxKind, ParticleSystem> systems = new Dictionary<FxKind, ParticleSystem>();
        private readonly List<PopupInstance> popups = new List<PopupInstance>();
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        private sealed class PopupInstance
        {
            public TextMesh Text, Shadow;
            public Vector3 Start;
            public float Age, Life = 1.25f, Scale = 1;
            public Color Color;
            public bool Active;
        }

        private void Awake()
        {
            if (ParticleMaterial == null) { Debug.LogError("FeedbackFx needs the particle material.", this); enabled = false; return; }
            // Systems, textures and the popup pool are created on first use so scene start-up stays cheap.
            if (PopupTemplate != null) PopupTemplate.gameObject.SetActive(false);
        }

        private ParticleSystem SystemFor(FxKind kind)
        {
            if (!systems.TryGetValue(kind, out var system) || system == null)
            {
                system = CreateSystem(kind);
                systems[kind] = system;
            }
            return system;
        }

        public void Burst(FxKind kind, Vector3 position, int count = -1)
        {
            var system = SystemFor(kind);
            if (system == null) return;
            var emit = new ParticleSystem.EmitParams { position = position };
            system.Emit(emit, count > 0 ? count : DefaultCount(kind));
        }

        public void Popup(string text, Vector3 position, Color color, float scale = 1)
        {
            if (PopupTemplate == null) return;
            if (popups.Count == 0) for (int i = 0; i < PopupPool; i++) popups.Add(CreatePopup());
            PopupInstance popup = null;
            foreach (var candidate in popups) if (!candidate.Active) { popup = candidate; break; }
            if (popup == null) popup = Oldest();
            popup.Active = true; popup.Age = 0; popup.Start = position; popup.Color = color; popup.Scale = scale;
            popup.Text.text = popup.Shadow.text = text;
            popup.Text.gameObject.SetActive(true); popup.Shadow.gameObject.SetActive(true);
        }

        private PopupInstance Oldest()
        {
            PopupInstance best = popups[0];
            foreach (var popup in popups) if (popup.Age > best.Age) best = popup;
            return best;
        }

        private void Update()
        {
            if (ViewCamera == null) return;
            foreach (var popup in popups)
            {
                if (!popup.Active) continue;
                popup.Age += Time.unscaledDeltaTime;
                float t = popup.Age / popup.Life;
                if (t >= 1) { popup.Active = false; popup.Text.gameObject.SetActive(false); popup.Shadow.gameObject.SetActive(false); continue; }
                float rise = 1 - Mathf.Pow(1 - t, 3);
                float pop = t < .14f ? Mathf.Lerp(.3f, 1.3f, t / .14f) : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((t - .14f) / .2f));
                float distance = Vector3.Distance(popup.Start, ViewCamera.transform.position);
                float size = pop * popup.Scale * Mathf.Clamp(distance / 5, .6f, 2.6f);
                Vector3 position = popup.Start + Vector3.up * (1.5f * rise);
                var rotation = ViewCamera.transform.rotation;
                float alpha = Mathf.Clamp01((1 - t) / .3f);
                popup.Text.transform.SetPositionAndRotation(position, rotation);
                popup.Shadow.transform.SetPositionAndRotation(position + rotation * new Vector3(.025f, -.025f, .01f) * size, rotation);
                popup.Text.transform.localScale = popup.Shadow.transform.localScale = Vector3.one * size;
                popup.Text.color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, alpha);
                popup.Shadow.color = new Color(0, 0, 0, alpha * .6f);
            }
        }

        private PopupInstance CreatePopup()
        {
            var popup = new PopupInstance();
            popup.Shadow = Object.Instantiate(PopupTemplate, transform);
            popup.Text = Object.Instantiate(PopupTemplate, transform);
            popup.Shadow.name = "PopupShadow"; popup.Text.name = "Popup";
            foreach (var mesh in new[] { popup.Shadow, popup.Text })
            {
                mesh.fontStyle = FontStyle.Bold;
                mesh.characterSize = .07f;
                mesh.gameObject.SetActive(false);
            }
            popup.Text.GetComponent<MeshRenderer>().sortingOrder = 11;
            popup.Shadow.GetComponent<MeshRenderer>().sortingOrder = 10;
            return popup;
        }

        private static int DefaultCount(FxKind kind)
        {
            switch (kind)
            {
                case FxKind.Sparkle: return 14;
                case FxKind.Hearts: return 6;
                case FxKind.Anger: return 6;
                case FxKind.Confetti: return 70;
                case FxKind.Coins: return 10;
                case FxKind.Splash: return 20;
                case FxKind.Smoke: return 8;
                case FxKind.Foam: return 3;
                case FxKind.Flame: return 2;
                default: return 9;
            }
        }

        private ParticleSystem CreateSystem(FxKind kind)
        {
            var go = new GameObject("Fx_" + kind);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false; main.loop = false; main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = true;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            string texture = "soft";
            var life = new ParticleSystem.MinMaxCurve(.6f, 1f);
            var speed = new ParticleSystem.MinMaxCurve(2f, 4f);
            var size = new ParticleSystem.MinMaxCurve(.12f, .25f);
            Gradient color = Fade(new Color(1, .86f, .3f), new Color(1, 1, .8f));
            float gravity = 0; bool grow = false, spin = false;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f;

            switch (kind)
            {
                case FxKind.Sparkle: texture = "star"; gravity = -.05f; break;
                case FxKind.Stars: texture = "star"; life = new ParticleSystem.MinMaxCurve(.8f, 1.1f); size = new ParticleSystem.MinMaxCurve(.22f, .38f); speed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f); spin = true; break;
                case FxKind.Hearts:
                    texture = "heart"; life = new ParticleSystem.MinMaxCurve(1.1f, 1.5f); speed = new ParticleSystem.MinMaxCurve(.9f, 1.8f);
                    size = new ParticleSystem.MinMaxCurve(.25f, .4f); color = Fade(new Color(1, .35f, .55f), new Color(1, .6f, .7f)); gravity = -.12f;
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 24; shape.rotation = new Vector3(-90, 0, 0); break;
                case FxKind.Anger:
                    life = new ParticleSystem.MinMaxCurve(.7f, 1f); speed = new ParticleSystem.MinMaxCurve(.5f, 1.2f); size = new ParticleSystem.MinMaxCurve(.3f, .5f);
                    color = Fade(new Color(.95f, .22f, .15f), new Color(.5f, .08f, .08f)); gravity = -.1f; grow = true; break;
                case FxKind.Confetti:
                    texture = "square"; life = new ParticleSystem.MinMaxCurve(1.8f, 2.6f); speed = new ParticleSystem.MinMaxCurve(5f, 9f);
                    size = new ParticleSystem.MinMaxCurve(.12f, .2f); gravity = .55f; spin = true;
                    color = Rainbow();
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 42; shape.rotation = new Vector3(-90, 0, 0); break;
                case FxKind.Coins:
                    life = new ParticleSystem.MinMaxCurve(.9f, 1.2f); speed = new ParticleSystem.MinMaxCurve(3f, 6f); size = new ParticleSystem.MinMaxCurve(.14f, .2f);
                    color = Fade(new Color(1, .8f, .15f), new Color(1, .95f, .5f)); gravity = 1.2f; texture = "coin";
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 34; shape.rotation = new Vector3(-90, 0, 0); break;
                case FxKind.Splash:
                    life = new ParticleSystem.MinMaxCurve(.6f, .9f); speed = new ParticleSystem.MinMaxCurve(2f, 5f); size = new ParticleSystem.MinMaxCurve(.1f, .22f);
                    color = Fade(new Color(.85f, .95f, 1f), new Color(1, 1, 1)); gravity = 1.6f;
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 30; shape.rotation = new Vector3(-90, 0, 0); break;
                case FxKind.Flame:
                    life = new ParticleSystem.MinMaxCurve(.3f, .5f); speed = new ParticleSystem.MinMaxCurve(.8f, 2f); size = new ParticleSystem.MinMaxCurve(.25f, .4f);
                    color = Fade(new Color(1, .75f, .2f), new Color(1, .25f, .1f)); gravity = -.3f;
                    shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 14; shape.radius = .08f; break;
                case FxKind.Foam:
                    life = new ParticleSystem.MinMaxCurve(1.3f, 2f); speed = new ParticleSystem.MinMaxCurve(.1f, .5f); size = new ParticleSystem.MinMaxCurve(.45f, .8f);
                    color = Fade(new Color(1, 1, 1, .55f), new Color(.85f, .96f, 1, .55f)); grow = true;
                    shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .25f; shape.rotation = new Vector3(-90, 0, 0); break;
                case FxKind.Smoke:
                    life = new ParticleSystem.MinMaxCurve(1.2f, 1.8f); speed = new ParticleSystem.MinMaxCurve(.4f, 1f); size = new ParticleSystem.MinMaxCurve(.5f, .8f);
                    color = Fade(new Color(.35f, .35f, .38f, .7f), new Color(.6f, .6f, .62f, .7f)); gravity = -.04f; grow = true; break;
            }
            main.startLifetime = life; main.startSpeed = speed; main.startSize = size;
            if (kind == FxKind.Confetti) main.startColor = new ParticleSystem.MinMaxGradient(color) { mode = ParticleSystemGradientMode.RandomColor };
            else main.startColor = new ParticleSystem.MinMaxGradient(color.colorKeys[0].color, color.colorKeys[1].color);
            main.gravityModifier = gravity;
            if (spin) { main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); var rotation = system.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-4, 4); }
            var colorOverLifetime = system.colorOverLifetime; colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(1, .6f), new GradientAlphaKey(0, 1) });
            colorOverLifetime.color = fade;
            var sizeOverLifetime = system.sizeOverLifetime; sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1, grow ? AnimationCurve.EaseInOut(0, .5f, 1, 1.6f) : AnimationCurve.Linear(0, 1, 1, .2f));
            renderer.sharedMaterial = TexturedMaterial(texture);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return system;
        }

        private static Gradient Fade(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(b.a, 1) });
            return g;
        }

        private static Gradient Rainbow()
        {
            var g = new Gradient();
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(1, .3f, .35f), 0), new GradientColorKey(new Color(1, .85f, .2f), .25f), new GradientColorKey(new Color(.3f, .85f, .4f), .5f),
                new GradientColorKey(new Color(.3f, .6f, 1f), .75f), new GradientColorKey(new Color(.85f, .4f, .95f), 1)
            }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return g;
        }

        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        private Material TexturedMaterial(string key)
        {
            if (materials.TryGetValue(key, out var existing)) return existing;
            var material = new Material(ParticleMaterial) { name = "Fx_" + key, mainTexture = Texture(key) };
            materials[key] = material;
            return material;
        }

        private static Texture2D Texture(string key)
        {
            if (Textures.TryGetValue(key, out var cached) && cached != null) return cached;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Fx_" + key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1;
                    float a = Shape(key, u, v);
                    pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
                }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Textures[key] = tex;
            return tex;
        }

        // Alpha of each sprite at (u, v) in [-1, 1]; edges are feathered so scaled-up particles stay soft.
        private static float Shape(string key, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            switch (key)
            {
                case "square": return Mathf.Clamp01((1 - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v) * 1.6f)) * 14);
                case "coin": return Mathf.Clamp01((.92f - r) * 12) * (r < .62f ? 1 : .8f) * (1 - .25f * Mathf.Clamp01(u * .8f + v * .6f));
                case "star":
                    {
                        float angle = Mathf.Atan2(v, u), spike = .5f + .5f * Mathf.Cos(angle * 5);
                        float edge = Mathf.Lerp(.32f, .95f, spike * spike);
                        return Mathf.Clamp01((edge - r) * 10) + Mathf.Clamp01((.28f - r) * 4) * .5f;
                    }
                case "heart":
                    {
                        float x = u * 1.25f, y = v * 1.25f + .1f;
                        float f = Mathf.Pow(x * x + y * y - 1, 3) - x * x * y * y * y;
                        return Mathf.Clamp01(-f * 7);
                    }
                default: return Mathf.Clamp01(1 - r) * Mathf.Clamp01(1 - r) * 1.6f;
            }
        }
    }
}
