using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsTentacleIndicator : MonoBehaviour
    {
        [SerializeField] private WarriorsTarget target;
        [SerializeField] private Font labelFont;
        [SerializeField] private Transform indicatorAnchor;

        /// <summary>이 촉수가 크라켄 팔에 묶여 있으면 그 다리. 없으면 예전처럼 고정 자리에 뜬다.</summary>
        private WarriorsTentacleArmLink armLink;
        [SerializeField] private TextMesh arrowLabel;
        [SerializeField] private SpriteRenderer arrowDisc;
        [SerializeField] private Color discColor = new(.06f, .10f, .22f, .93f);
        [SerializeField, Min(.1f)] private float discSize = .86f;
        [SerializeField] private SpriteRenderer windowRing;
        [SerializeField] private Color glyphColor = new(.97f, .99f, 1f, 1f);
        [SerializeField] private Color ringColor = new(1f, .72f, .26f, .95f);
        [SerializeField, Min(.1f)] private float ringStartSize = 1.4f;

        /// <summary>
        /// 표식 전체(원판 · 링 · 화살표)를 한꺼번에 키우는 값.
        ///
        /// ⚠ <b>원판만 키우면 안 된다.</b> 링은 <see cref="ringStartSize"/> 에서 <see cref="discSize"/> 까지
        ///    조여들며 남은 시간을 보여 주는데, 원판만 키우면 그 여백이 사라져 <b>조여드는 것이 안 보인다.</b>
        ///    실제로 그렇게 만들었다가 되돌렸다. 멀어서 작게 보이면 이 값으로 통째로 키운다.
        /// </summary>
        [SerializeField, Min(.1f)] private float markerScale = 1f;

        /// <summary>맞은 순간 부풀며 사라지는 세기. 1(막 맞음) → 0(다 사라짐).</summary>
        private float burst;

        /// <summary>이미 터졌는가. 터진 표식은 다시 나타나지 않는다 — 다음 촉수가 올라올 때 되살아난다.</summary>
        private bool spent;

        /// <summary>직전 프레임의 남은 시간. 이 값이 <b>다시 올라가면</b> 새 촉수가 선 것이다.</summary>
        private float lastRemaining = -1f;
        [SerializeField] private Vector3 localOffset = new(0f, 3.6f, -.82f);
        [SerializeField] private Vector3 fixedWorldEuler = new(0f, 180f, 0f);
        [SerializeField, Min(0f)] private float cameraClearance = 1f;
        [SerializeField] private float outwardOffset = 1f;

        /// <summary>터지는 연출 길이(초).</summary>
        private const float BurstSeconds = .45f;

        private Transform bodyCentre;

        /// <summary>맞았다 — 표식이 한 번 부풀었다 옅어진다. 팔은 움직이지 않는다.</summary>
        public void PlayBurst()
        {
            burst = 1f;
            spent = true;

            SpawnShards(indicatorAnchor != null ? indicatorAnchor.position : transform.position);
        }

        /// <summary>
        /// 표식이 터질 때 튀는 <b>분홍 파편.</b> 프리팹 없이 코드로 만든다
        /// (촉수 물보라 <c>WarriorsKrakenBoss.SpawnTentacleSplash</c> 와 같은 방식).
        ///
        /// 크기만 커지는 연출은 화면에서 거의 안 보였다. 실제로 <b>부서지는 것이 튀어야</b>
        /// "제대로 맞혔다" 가 읽힌다. 색은 크라켄 빨판의 분홍에서 가져왔다.
        /// </summary>
        private static void SpawnShards(Vector3 position)
        {
            var shards = new GameObject("TentacleMarkShards");
            shards.transform.position = position;

            ParticleSystem particles = shards.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = .2f;
            main.loop = false;
            main.startLifetime = .3f;
            main.startSpeed = 3.2f;
            main.startSize = .3f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, .62f, .82f, 1f), new Color(1f, .86f, .93f, 1f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            main.gravityModifier = .55f;
            main.maxParticles = 4;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });   // 톡 하고 두 조각만. 많으면 과하다

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .12f;

            // 튀어 나가며 작아진다 — 파편처럼 보이게.
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) },
            });

            // 표식보다 앞에 그려져야 보인다. 파티클 기본 재질은 렌더 큐가 뒤라 바다에 잠긴다.
            var renderer = shards.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 33;
            }

            particles.Play();
        }

        private Vector3 BodyCentre
        {
            get
            {
                if (bodyCentre == null)
                {
                    var boss = GetComponentInParent<WarriorsKrakenBoss>(true);
                    bodyCentre = boss != null ? boss.transform : transform.parent;
                }
                return bodyCentre != null ? bodyCentre.position : Vector3.zero;
            }
        }

        private void Awake()
        {
            if (target == null) target = GetComponent<WarriorsTarget>();
            EnsureLabel();
        }

        private void OnEnable()
        {
            // 촉수는 자리를 돌려 쓴다. 지난번에 터진 상태가 남아 있으면 새로 올라온 촉수가
            // 검은 원판으로 보인다 — 실제로 세 번째 촉수부터 그랬다.
            burst = 0f;
            spent = false;
            lastRemaining = -1f;

            EnsureLabel();
            RefreshLabel();
        }

        private void LateUpdate()
        {
            if (arrowLabel == null) EnsureLabel();
            if (arrowLabel == null) return;
            // 팔에 묶여 있으면(2라운드 크라켄 팔) **그 팔 끝을 따라간다.** 팔이 흔들리는데 표식만
            // 제자리에 있으면 둘이 따로 놀아 무엇을 베는지 안 읽힌다 — 화면에서 실제로 그랬다.
            if (armLink == null) armLink = GetComponentInParent<WarriorsTentacleArmLink>();

            if (armLink != null && armLink.TryTipPosition(out Vector3 tip)) indicatorAnchor.position = tip;
            else indicatorAnchor.localPosition = localOffset;

            indicatorAnchor.rotation = Quaternion.Euler(fixedWorldEuler);

            // The arms fill the frame, so a marker sitting dead centre on one of them
            // reads as part of the arm rather than as something to answer. Stepping it
            // out to the side - away from the body - gives it its own patch of screen
            // while it still clearly belongs to the arm underneath it.
            // ⚠ 팔에 묶인 촉수는 옆으로 밀지 않는다. 그 값은 표식을 팔에서 **떼어 놓으려던** 것이라,
            //    팔 위에 얹는 지금 방식과는 반대로 작동한다.
            if (outwardOffset != 0f && armLink == null)
            {
                float side = indicatorAnchor.position.x - BodyCentre.x;
                indicatorAnchor.position +=
                    Vector3.right * ((side < 0f ? -1f : 1f) * outwardOffset);
            }

            // The marker sat three centimetres proud of the arm it belongs to, which is
            // nothing beside the thickness of the kraken's head - so the ring kept being
            // sliced in half by the body behind it. Floating the whole marker toward the
            // camera clears the mesh, and at this distance it barely moves on screen.
            Camera view = Camera.main;
            if (view != null && cameraClearance > 0f)
            {
                Vector3 toCamera = view.transform.position - indicatorAnchor.position;
                if (toCamera.sqrMagnitude > .0001f)
                    indicatorAnchor.position += toCamera.normalized * cameraClearance;
            }

            indicatorAnchor.gameObject.SetActive(target != null && !target.IsDefeated);

            // 맞은 순간 표식이 **부풀면서 옅어진다.** 한 번 터진 표식은 다시 나타나지 않는다.
            //
            // ⚠ 예전에는 알파를 (1 - burst) 로 줬는데, 맞는 순간 값이 1 이라 **즉시 사라졌다가
            //    잦아들면서 도로 나타났다.** 화면에서 "벴는데 화살표가 남아 있다" 로 보였다.
            //    지금은 burst 가 그대로 알파다 — 1(선명) → 0(사라짐).
            if (burst > 0f) burst = Mathf.Max(0f, burst - Time.unscaledDeltaTime / BurstSeconds);

            // 터질 때는 **크게, 그리고 늦게 옅어진다.** 작게 터지면 화면에서 안 보인다 —
            // 제곱근을 써서 커지는 동안에도 한참 선명하게 남는다.
            float show = spent ? Mathf.Sqrt(burst) : 1f;
            float pop = 1f + 3f * (spent ? 1f - burst : 0f);

            indicatorAnchor.localScale = Vector3.one * (markerScale * pop);

            if (arrowDisc != null)
            {
                arrowDisc.transform.localScale = Vector3.one * discSize;
                Color tone = discColor;
                arrowDisc.color = new Color(tone.r, tone.g, tone.b, tone.a * show);
            }

            // The ring closes in on the marker as the window runs out and meets it at the
            // moment the tentacle swings. No colour change and no number: the distance
            // between the ring and the disc is the reading.
            if (windowRing != null)
            {
                float remaining = target != null ? target.StrikeWindowNormalized : -1f;

                // 남은 시간이 **다시 올라갔다** = 같은 자리에 새 촉수가 섰다. 표식을 되살린다.
                if (remaining > lastRemaining + .05f) { spent = false; burst = 0f; }
                lastRemaining = remaining;

                windowRing.enabled = remaining >= 0f && (!spent || burst > 0f);

                if (remaining >= 0f)
                {
                    windowRing.transform.localScale =
                        Vector3.one * Mathf.Lerp(discSize, ringStartSize, remaining);

                    // 막 올라온 순간 링이 가장 크다. 그대로 켜면 튀어나오는 것처럼 보여서
                    // 처음 0.2초는 알파로 스며들게 한다. 터질 때는 같이 옅어진다.
                    float fadeIn = Mathf.InverseLerp(1f, .97f, remaining);
                    Color tone = ringColor;
                    windowRing.color = new Color(tone.r, tone.g, tone.b, tone.a * fadeIn * show);
                }
            }
            if (arrowLabel != null)
            {
                Color glyph = glyphColor;
                arrowLabel.color = new Color(glyph.r, glyph.g, glyph.b, glyph.a * show);
            }

            RefreshLabel();
        }

        /// <summary>
        /// The weakness arrow sits on a navy disc so it reads as a badge held in front of
        /// the arm rather than a glyph painted onto it - the same dark card the HUD uses
        /// everywhere else, which is what makes a white arrow carry across the purple arm,
        /// the blue sea and the sky behind them.
        /// The disc is generated rather than imported so it needs no art asset.
        /// </summary>
        private void EnsureDisc()
        {
            if (indicatorAnchor == null) return;

            if (arrowDisc == null)
            {
                Transform existing = indicatorAnchor.Find("AttackIndicatorDisc");
                if (existing != null) arrowDisc = existing.GetComponent<SpriteRenderer>();
            }
            if (arrowDisc == null)
            {
                GameObject discObject = new("AttackIndicatorDisc");
                discObject.transform.SetParent(indicatorAnchor, false);
                arrowDisc = discObject.AddComponent<SpriteRenderer>();
            }

            arrowDisc.sprite = CreateDiscSprite();
            arrowDisc.sharedMaterial = DiscMaterial() ?? arrowDisc.sharedMaterial;
            arrowDisc.color = discColor;
            // Sorting order outranks the render queue, so a negative order put the disc
            // and the ring behind the sea no matter what queue the shader asked for -
            // which is why the marker kept coming back sliced along the waterline while
            // the glyph, which sat above it, always survived. All three ride above the
            // scene now, stacked ring - disc - glyph.
            arrowDisc.sortingOrder = 31;
            arrowDisc.transform.localPosition = new Vector3(0f, 0f, .02f);
            arrowDisc.transform.localScale = Vector3.one * discSize;
            arrowDisc.transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// Draws with the depth test off, so the marker is never sliced by the arm it is
        /// pinned to. Every piece gets its own material: a SpriteRenderer binds its own
        /// texture into whatever material it draws with, so handing the disc and the ring
        /// one shared instance left both of them drawing whichever texture was bound last
        /// - which is why the disc came out as a hollow outline instead of a solid backing.
        /// </summary>
        private static Material overlayTemplate;
        private static Material discMaterial;
        private static Material ringMaterial;
        private static Material glyphMaterial;

        private static Material OverlayMaterial(Texture texture)
        {
            if (overlayTemplate == null)
            {
                Shader shader = Shader.Find("Warriors/OverlaySprite");
                if (shader == null) return null;
                overlayTemplate = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            return new Material(overlayTemplate)
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture
            };
        }

        private static Material DiscMaterial()
        {
            if (discMaterial == null) discMaterial = OverlayMaterial(CreateDiscSprite().texture);
            return discMaterial;
        }

        private static Material RingMaterial()
        {
            if (ringMaterial == null) ringMaterial = OverlayMaterial(CreateRingSprite().texture);
            return ringMaterial;
        }

        private static Material GlyphMaterial(Material fontMaterial)
        {
            if (glyphMaterial != null) return glyphMaterial;
            if (fontMaterial == null) return null;
            glyphMaterial = OverlayMaterial(fontMaterial.mainTexture);
            // The glyph comes from a font atlas, which carries the letter in its alpha and
            // leaves the colour channels black - without this the arrow drew as a black
            // blob instead of taking the colour it is told to be.
            if (glyphMaterial != null) glyphMaterial.SetFloat("_AlphaOnly", 1f);
            return glyphMaterial;
        }

        private static Sprite discSprite;

        private static Sprite CreateDiscSprite()
        {
            if (discSprite != null) return discSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WarriorsIndicatorDisc",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float centre = (size - 1) * .5f;
            float radius = centre - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    // One pixel of feathering keeps the rim smooth at any distance.
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            discSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            discSprite.name = "WarriorsIndicatorDisc";
            discSprite.hideFlags = HideFlags.HideAndDontSave;
            return discSprite;
        }

        /// <summary>
        /// Generated like the disc so it needs no imported art, but as an annulus - it has
        /// to read as a closing ring rather than a growing blob.
        /// </summary>
        private void EnsureRing()
        {
            if (indicatorAnchor == null) return;

            if (windowRing == null)
            {
                Transform existing = indicatorAnchor.Find("AttackWindowRing");
                if (existing != null) windowRing = existing.GetComponent<SpriteRenderer>();
            }
            if (windowRing == null)
            {
                GameObject ringObject = new("AttackWindowRing");
                ringObject.transform.SetParent(indicatorAnchor, false);
                windowRing = ringObject.AddComponent<SpriteRenderer>();
            }

            windowRing.sprite = CreateRingSprite();
            windowRing.sharedMaterial = RingMaterial() ?? windowRing.sharedMaterial;
            windowRing.color = ringColor;
            windowRing.sortingOrder = 30;
            windowRing.transform.localPosition = new Vector3(0f, 0f, .03f);
            windowRing.transform.localRotation = Quaternion.identity;
        }

        private static Sprite ringSprite;

        private static Sprite CreateRingSprite()
        {
            if (ringSprite != null) return ringSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "WarriorsIndicatorRing",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            float centre = (size - 1) * .5f;
            float outer = centre - 1f;
            float inner = outer * .80f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    float alpha = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
            ringSprite.name = "WarriorsIndicatorRing";
            ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return ringSprite;
        }

        private void EnsureLabel()
        {
            if (indicatorAnchor == null)
            {
                indicatorAnchor = transform.Find("IndicatorAnchor");
                if (indicatorAnchor == null) indicatorAnchor = transform.Find("DirectionIndicatorAnchor");
            }
            if (indicatorAnchor == null)
            {
                GameObject anchorObject = new("IndicatorAnchor");
                indicatorAnchor = anchorObject.transform;
                indicatorAnchor.SetParent(transform, false);
            }

            EnsureDisc();
            EnsureRing();
            if (arrowLabel == null) arrowLabel = indicatorAnchor.GetComponentInChildren<TextMesh>(true);
            if (arrowLabel == null)
            {
                GameObject labelObject = new("AttackIndicator");
                labelObject.transform.SetParent(indicatorAnchor, false);
                arrowLabel = labelObject.AddComponent<TextMesh>();
            }
            arrowLabel.anchor = TextAnchor.MiddleCenter;
            arrowLabel.alignment = TextAlignment.Center;
            arrowLabel.fontSize = 64;
            // Sized to sit inside the disc rather than spill over its rim - at the old
            // size the glyph was wider than the marker it was supposed to be sitting in.
            arrowLabel.characterSize = .075f;
            arrowLabel.color = glyphColor;
            arrowLabel.fontStyle = FontStyle.Bold;
            if (labelFont != null)
            {
                arrowLabel.font = labelFont;
                // The glyph joins the ring and the disc on the overlay pass, carrying the
                // font atlas across so it still draws letters rather than a white quad.
                arrowLabel.GetComponent<MeshRenderer>().sharedMaterial =
                    GlyphMaterial(labelFont.material) ?? labelFont.material;
            }
            indicatorAnchor.localPosition = localOffset;
            indicatorAnchor.rotation = Quaternion.Euler(fixedWorldEuler);
            arrowLabel.transform.localPosition = Vector3.zero;
            arrowLabel.transform.localRotation = Quaternion.identity;
            arrowLabel.GetComponent<MeshRenderer>().sortingOrder = 32;
        }

        private void RefreshLabel()
        {
            if (arrowLabel == null || target == null) return;
            arrowLabel.text = target.RequiredDirection switch
            {
                WarriorsAttackDirection.HorizontalSlash => "↔",
                WarriorsAttackDirection.VerticalSlash => "↕",
                _ => "⊙"
            };
        }
    }
}
