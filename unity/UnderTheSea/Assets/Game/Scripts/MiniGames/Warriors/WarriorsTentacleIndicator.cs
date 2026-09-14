using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsTentacleIndicator : MonoBehaviour
    {
        [SerializeField] private WarriorsTarget target;
        [SerializeField] private Font labelFont;
        [SerializeField] private Transform indicatorAnchor;
        [SerializeField] private TextMesh arrowLabel;
        [SerializeField] private SpriteRenderer arrowDisc;
        [SerializeField] private Color discColor = new(.06f, .10f, .22f, .93f);
        [SerializeField, Min(.1f)] private float discSize = .86f;
        [SerializeField] private SpriteRenderer windowRing;
        [SerializeField] private Color glyphColor = new(.97f, .99f, 1f, 1f);
        [SerializeField] private Color ringColor = new(1f, .72f, .26f, .95f);
        [SerializeField, Min(.1f)] private float ringStartSize = 1.78f;
        [SerializeField] private Vector3 localOffset = new(0f, 3.6f, -.82f);
        [SerializeField] private Vector3 fixedWorldEuler = new(0f, 180f, 0f);
        [SerializeField, Min(0f)] private float cameraClearance = 1f;
        [SerializeField] private float outwardOffset = 1f;

        private Transform bodyCentre;

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
            EnsureLabel();
            RefreshLabel();
        }

        private void LateUpdate()
        {
            if (arrowLabel == null) EnsureLabel();
            if (arrowLabel == null) return;
            indicatorAnchor.localPosition = localOffset;
            indicatorAnchor.rotation = Quaternion.Euler(fixedWorldEuler);

            // The arms fill the frame, so a marker sitting dead centre on one of them
            // reads as part of the arm rather than as something to answer. Stepping it
            // out to the side - away from the body - gives it its own patch of screen
            // while it still clearly belongs to the arm underneath it.
            if (outwardOffset != 0f)
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
            if (arrowDisc != null) arrowDisc.transform.localScale = Vector3.one * discSize;

            // The ring closes in on the marker as the window runs out and meets it at the
            // moment the tentacle swings. No colour change and no number: the distance
            // between the ring and the disc is the reading.
            if (windowRing != null)
            {
                float remaining = target != null ? target.StrikeWindowNormalized : -1f;
                windowRing.enabled = remaining >= 0f;
                if (remaining >= 0f)
                    windowRing.transform.localScale =
                        Vector3.one * Mathf.Lerp(discSize, ringStartSize, remaining);
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
