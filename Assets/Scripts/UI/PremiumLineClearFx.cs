using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ChromaBlast
{
    /// <summary>
    /// Runtime-only UI effect for line clears. It does not alter the board state,
    /// scoring, clear timing, scene hierarchy, prefabs, or saved data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PremiumLineClearFx : MonoBehaviour
    {
        private const int BoardSize = 8;
        private const int BeamPoolSize = 12;
        private const int ParticlePoolSize = 32;
        private const float BeamExpandDuration = 0.115f;
        private const float BeamFadeDuration = 0.17f;

        private sealed class BeamItem
        {
            public RectTransform root;
            public Image glow;
            public Image core;
            public Image head;
            public Coroutine routine;
        }

        private sealed class ParticleItem
        {
            public RectTransform rect;
            public Image image;
            public Coroutine routine;
        }

        private readonly List<BeamItem> beams = new List<BeamItem>(BeamPoolSize);
        private readonly List<ParticleItem> particles = new List<ParticleItem>(ParticlePoolSize);
        private readonly List<int> clearedRows = new List<int>(BoardSize);
        private readonly List<int> clearedColumns = new List<int>(BoardSize);
        private readonly List<Vector2Int> clearedCells = new List<Vector2Int>(BoardSize * 2);
        private readonly Vector3[] worldCorners = new Vector3[4];

        private RectTransform layerRect;
        private Canvas ownerCanvas;
        private int nextBeam;
        private int nextParticle;

        public void Initialize(RectTransform targetLayer)
        {
            layerRect = targetLayer != null ? targetLayer : transform as RectTransform;
            ownerCanvas = GetComponentInParent<Canvas>();
            EnsurePools();
        }

        public void Play(RectTransform boardRect, ClearResult result, Vector2 fallbackScreenPosition, Color accentColor, int chain)
        {
            if (result == null || result.linesCleared <= 0)
            {
                return;
            }

            if (layerRect == null)
            {
                Initialize(transform as RectTransform);
            }

            if (layerRect == null)
            {
                return;
            }

            EnsurePools();
            layerRect.SetAsLastSibling();

            GetBoardBounds(boardRect, fallbackScreenPosition, out Rect boardBounds, out Vector2 fallbackLocalPosition);
            ExtractClearGeometry(result, clearedRows, clearedColumns, clearedCells);
            DeriveLinesFromCells(clearedCells, clearedRows, clearedColumns);
            SanitizeIndices(clearedRows);
            SanitizeIndices(clearedColumns);

            int requestedLines = Mathf.Clamp(result.linesCleared, 1, BoardSize * 2);
            int exactLineCount = clearedRows.Count + clearedColumns.Count;
            float strength = Mathf.Clamp01(
                0.82f
                + Mathf.Max(0, requestedLines - 1) * 0.09f
                + Mathf.Max(0, result.pureLines) * 0.04f
                + Mathf.Max(0, chain - 1) * 0.045f);

            if (exactLineCount > 0)
            {
                for (int i = 0; i < clearedRows.Count; i++)
                {
                    float y = boardBounds.yMin + ((clearedRows[i] + 0.5f) / BoardSize) * boardBounds.height;
                    PlayBeam(new Vector2(boardBounds.center.x, y), boardBounds.width, true, accentColor, strength, i);
                }

                for (int i = 0; i < clearedColumns.Count; i++)
                {
                    float x = boardBounds.xMin + ((clearedColumns[i] + 0.5f) / BoardSize) * boardBounds.width;
                    PlayBeam(new Vector2(x, boardBounds.center.y), boardBounds.height, false, accentColor, strength, clearedRows.Count + i);
                }
            }
            else
            {
                PlayFallbackBeams(boardBounds, fallbackLocalPosition, requestedLines, accentColor, strength);
            }

            PlayCenterFlare(fallbackLocalPosition, accentColor, requestedLines, chain);
        }

        private void EnsurePools()
        {
            if (layerRect == null)
            {
                return;
            }

            while (beams.Count < BeamPoolSize)
            {
                beams.Add(CreateBeam(beams.Count));
            }

            while (particles.Count < ParticlePoolSize)
            {
                particles.Add(CreateParticle(particles.Count));
            }
        }

        private BeamItem CreateBeam(int index)
        {
            GameObject rootObject = new GameObject($"LineBeam_{index:00}", typeof(RectTransform));
            rootObject.transform.SetParent(layerRect, false);
            RectTransform root = (RectTransform)rootObject.transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0f, 0.5f);
            root.localScale = Vector3.one;

            Image glow = CreateImageChild(root, "Glow");
            RectTransform glowRect = (RectTransform)glow.transform;
            Stretch(glowRect);

            Image core = CreateImageChild(root, "Core");
            RectTransform coreRect = (RectTransform)core.transform;
            coreRect.anchorMin = new Vector2(0f, 0.5f);
            coreRect.anchorMax = new Vector2(1f, 0.5f);
            coreRect.pivot = new Vector2(0.5f, 0.5f);
            coreRect.offsetMin = new Vector2(0f, -2.2f);
            coreRect.offsetMax = new Vector2(0f, 2.2f);

            Image head = CreateImageChild(root, "HeadFlare");
            RectTransform headRect = (RectTransform)head.transform;
            headRect.anchorMin = new Vector2(1f, 0.5f);
            headRect.anchorMax = new Vector2(1f, 0.5f);
            headRect.pivot = new Vector2(0.5f, 0.5f);
            headRect.sizeDelta = new Vector2(18f, 18f);
            headRect.anchoredPosition = Vector2.zero;
            headRect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            rootObject.SetActive(false);
            return new BeamItem
            {
                root = root,
                glow = glow,
                core = core,
                head = head
            };
        }

        private ParticleItem CreateParticle(int index)
        {
            GameObject particleObject = new GameObject($"ClearParticle_{index:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            particleObject.transform.SetParent(layerRect, false);
            RectTransform rect = (RectTransform)particleObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(8f, 8f);

            Image image = particleObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.clear;
            particleObject.SetActive(false);

            return new ParticleItem
            {
                rect = rect,
                image = image
            };
        }

        private static Image CreateImageChild(RectTransform parent, string objectName)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void PlayBeam(Vector2 center, float length, bool horizontal, Color accentColor, float strength, int order)
        {
            if (length <= 1f || beams.Count == 0)
            {
                return;
            }

            BeamItem beam = beams[nextBeam % beams.Count];
            nextBeam++;
            if (beam.routine != null)
            {
                StopCoroutine(beam.routine);
            }

            beam.routine = StartCoroutine(AnimateBeam(beam, center, length, horizontal, accentColor, strength, order));
        }

        private IEnumerator AnimateBeam(BeamItem beam, Vector2 center, float length, bool horizontal, Color accentColor, float strength, int order)
        {
            RectTransform root = beam.root;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();

            bool forward = order % 2 == 0;
            float bandThickness = MobilePerformance.LowEndMode ? 14f : 20f;
            root.pivot = horizontal
                ? new Vector2(forward ? 0f : 1f, 0.5f)
                : new Vector2(0.5f, forward ? 0f : 1f);
            root.sizeDelta = horizontal
                ? new Vector2(length, bandThickness)
                : new Vector2(bandThickness, length);
            root.anchoredPosition = horizontal
                ? new Vector2(center.x + (forward ? -length * 0.5f : length * 0.5f), center.y)
                : new Vector2(center.x, center.y + (forward ? -length * 0.5f : length * 0.5f));
            root.localRotation = Quaternion.identity;
            root.localScale = horizontal ? new Vector3(0.025f, 1f, 1f) : new Vector3(1f, 0.025f, 1f);

            RectTransform headRect = (RectTransform)beam.head.transform;
            headRect.anchorMin = horizontal
                ? new Vector2(forward ? 1f : 0f, 0.5f)
                : new Vector2(0.5f, forward ? 1f : 0f);
            headRect.anchorMax = headRect.anchorMin;
            headRect.sizeDelta = MobilePerformance.LowEndMode ? new Vector2(14f, 14f) : new Vector2(21f, 21f);

            Color glowColor = Color.Lerp(accentColor, Color.white, 0.16f);
            glowColor.a = 0f;
            Color coreColor = Color.Lerp(accentColor, Color.white, 0.72f);
            coreColor.a = 0f;
            Color headColor = Color.Lerp(accentColor, Color.white, 0.88f);
            headColor.a = 0f;
            beam.glow.color = glowColor;
            beam.core.color = coreColor;
            beam.head.color = headColor;

            float elapsed = 0f;
            while (elapsed < BeamExpandDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BeamExpandDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                root.localScale = horizontal
                    ? new Vector3(Mathf.LerpUnclamped(0.025f, 1f, eased), 1f, 1f)
                    : new Vector3(1f, Mathf.LerpUnclamped(0.025f, 1f, eased), 1f);

                glowColor.a = Mathf.Sin(t * Mathf.PI * 0.82f) * 0.34f * strength;
                coreColor.a = Mathf.Sin(t * Mathf.PI * 0.82f) * 0.93f * strength;
                headColor.a = Mathf.Sin(t * Mathf.PI) * strength;
                beam.glow.color = glowColor;
                beam.core.color = coreColor;
                beam.head.color = headColor;
                yield return null;
            }

            SpawnLineParticles(center, length, horizontal, accentColor, strength);

            elapsed = 0f;
            Vector3 fullScale = Vector3.one;
            Vector3 endScale = horizontal ? new Vector3(1.055f, 0.72f, 1f) : new Vector3(0.72f, 1.055f, 1f);
            while (elapsed < BeamFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / BeamFadeDuration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                root.localScale = Vector3.LerpUnclamped(fullScale, endScale, eased);
                glowColor.a = Mathf.Lerp(0.30f * strength, 0f, eased);
                coreColor.a = Mathf.Lerp(0.82f * strength, 0f, eased);
                headColor.a = Mathf.Lerp(0.54f * strength, 0f, eased);
                beam.glow.color = glowColor;
                beam.core.color = coreColor;
                beam.head.color = headColor;
                yield return null;
            }

            beam.glow.color = Color.clear;
            beam.core.color = Color.clear;
            beam.head.color = Color.clear;
            root.gameObject.SetActive(false);
            beam.routine = null;
        }

        private void PlayFallbackBeams(Rect bounds, Vector2 fallbackLocalPosition, int lineCount, Color accentColor, float strength)
        {
            Vector2 clamped = new Vector2(
                Mathf.Clamp(fallbackLocalPosition.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(fallbackLocalPosition.y, bounds.yMin, bounds.yMax));

            if (lineCount <= 1)
            {
                PlayBeam(new Vector2(bounds.center.x, clamped.y), bounds.width, true, accentColor, strength * 0.72f, 0);
                PlayBeam(new Vector2(clamped.x, bounds.center.y), bounds.height, false, accentColor, strength * 0.48f, 1);
                return;
            }

            int count = Mathf.Min(lineCount, 6);
            for (int i = 0; i < count; i++)
            {
                bool horizontal = i % 2 == 0;
                float offset = (i / 2 - (count - 1) * 0.16f) * Mathf.Min(bounds.width, bounds.height) / BoardSize;
                if (horizontal)
                {
                    PlayBeam(new Vector2(bounds.center.x, Mathf.Clamp(clamped.y + offset, bounds.yMin, bounds.yMax)), bounds.width, true, accentColor, strength * 0.70f, i);
                }
                else
                {
                    PlayBeam(new Vector2(Mathf.Clamp(clamped.x + offset, bounds.xMin, bounds.xMax), bounds.center.y), bounds.height, false, accentColor, strength * 0.70f, i);
                }
            }
        }

        private void SpawnLineParticles(Vector2 center, float length, bool horizontal, Color accentColor, float strength)
        {
            int count = MobilePerformance.LowEndMode ? 3 : 6;
            for (int i = 0; i < count; i++)
            {
                float along = UnityEngine.Random.Range(-0.43f, 0.43f) * length;
                Vector2 start = center + (horizontal ? Vector2.right * along : Vector2.up * along);
                Vector2 outward = horizontal
                    ? new Vector2(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(-58f, 58f))
                    : new Vector2(UnityEngine.Random.Range(-58f, 58f), UnityEngine.Random.Range(-18f, 18f));
                float size = UnityEngine.Random.Range(5f, MobilePerformance.LowEndMode ? 8f : 11f);
                PlayParticle(start, outward, size, accentColor, UnityEngine.Random.Range(0.24f, 0.38f), strength);
            }
        }

        private void PlayCenterFlare(Vector2 position, Color accentColor, int lineCount, int chain)
        {
            int count = MobilePerformance.LowEndMode ? 3 : Mathf.Clamp(5 + lineCount + Mathf.Max(0, chain - 1), 6, 13);
            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i + UnityEngine.Random.Range(-12f, 12f);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                float distance = UnityEngine.Random.Range(42f, MobilePerformance.LowEndMode ? 72f : 105f);
                float size = i == 0 ? 20f : UnityEngine.Random.Range(6f, 13f);
                PlayParticle(position, direction * distance, size, accentColor, UnityEngine.Random.Range(0.26f, 0.44f), 1f);
            }
        }

        private void PlayParticle(Vector2 start, Vector2 movement, float size, Color accentColor, float duration, float strength)
        {
            if (particles.Count == 0)
            {
                return;
            }

            ParticleItem particle = particles[nextParticle % particles.Count];
            nextParticle++;
            if (particle.routine != null)
            {
                StopCoroutine(particle.routine);
            }

            particle.routine = StartCoroutine(AnimateParticle(particle, start, movement, size, accentColor, duration, strength));
        }

        private IEnumerator AnimateParticle(ParticleItem particle, Vector2 start, Vector2 movement, float size, Color accentColor, float duration, float strength)
        {
            RectTransform rect = particle.rect;
            rect.gameObject.SetActive(true);
            rect.SetAsLastSibling();
            rect.anchoredPosition = start;
            rect.sizeDelta = new Vector2(size, size);
            rect.localScale = Vector3.one * 0.72f;
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 90f));

            Color color = Color.Lerp(accentColor, Color.white, UnityEngine.Random.Range(0.30f, 0.76f));
            color.a = Mathf.Clamp01(0.92f * strength);
            particle.image.color = color;

            float elapsed = 0f;
            float spin = UnityEngine.Random.Range(-190f, 190f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                rect.anchoredPosition = Vector2.LerpUnclamped(start, start + movement, eased);
                rect.localRotation = Quaternion.Euler(0f, 0f, spin * t);
                rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 0.18f, t);
                color.a = Mathf.Lerp(0.92f * strength, 0f, t * t);
                particle.image.color = color;
                yield return null;
            }

            particle.image.color = Color.clear;
            rect.gameObject.SetActive(false);
            particle.routine = null;
        }

        private void GetBoardBounds(RectTransform boardRect, Vector2 fallbackScreenPosition, out Rect bounds, out Vector2 fallbackLocalPosition)
        {
            fallbackLocalPosition = ScreenToLayerPoint(fallbackScreenPosition);

            if (boardRect != null)
            {
                boardRect.GetWorldCorners(worldCorners);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < worldCorners.Length; i++)
                {
                    Vector3 local = layerRect.InverseTransformPoint(worldCorners[i]);
                    min.x = Mathf.Min(min.x, local.x);
                    min.y = Mathf.Min(min.y, local.y);
                    max.x = Mathf.Max(max.x, local.x);
                    max.y = Mathf.Max(max.y, local.y);
                }

                if (max.x - min.x > 40f && max.y - min.y > 40f)
                {
                    bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                    if (fallbackScreenPosition == Vector2.zero)
                    {
                        fallbackLocalPosition = bounds.center;
                    }

                    return;
                }
            }

            float fallbackSize = Mathf.Clamp(Mathf.Min(layerRect.rect.width, layerRect.rect.height) * 0.58f, 360f, 720f);
            Vector2 center = fallbackScreenPosition == Vector2.zero ? Vector2.zero : fallbackLocalPosition;
            bounds = new Rect(center.x - fallbackSize * 0.5f, center.y - fallbackSize * 0.5f, fallbackSize, fallbackSize);
            fallbackLocalPosition = center;
        }

        private Vector2 ScreenToLayerPoint(Vector2 screenPosition)
        {
            if (screenPosition == Vector2.zero || layerRect == null)
            {
                return Vector2.zero;
            }

            Camera camera = null;
            if (ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                camera = ownerCanvas.worldCamera;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(layerRect, screenPosition, camera, out Vector2 local)
                ? local
                : Vector2.zero;
        }

        private static void ExtractClearGeometry(object result, List<int> rows, List<int> columns, List<Vector2Int> cells)
        {
            rows.Clear();
            columns.Clear();
            cells.Clear();
            if (result == null)
            {
                return;
            }

            Type type = result.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                object value;
                try
                {
                    value = fields[i].GetValue(result);
                }
                catch
                {
                    continue;
                }

                InspectMember(fields[i].Name, value, rows, columns, cells);
            }

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                if (!properties[i].CanRead || properties[i].GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object value;
                try
                {
                    value = properties[i].GetValue(result, null);
                }
                catch
                {
                    continue;
                }

                InspectMember(properties[i].Name, value, rows, columns, cells);
            }
        }

        private static void InspectMember(string memberName, object value, List<int> rows, List<int> columns, List<Vector2Int> cells)
        {
            if (value == null || string.IsNullOrEmpty(memberName))
            {
                return;
            }

            string lowerName = memberName.ToLowerInvariant();
            bool rowLike = lowerName.Contains("row") || lowerName.Contains("horizontal");
            bool columnLike = lowerName.Contains("column") || lowerName.Contains("vertical") || lowerName == "cols" || lowerName.Contains("colindices");
            bool cellLike = lowerName.Contains("cell") || lowerName.Contains("coord") || lowerName.Contains("position") || lowerName.Contains("tile") || lowerName.Contains("cleared");

            if (rowLike)
            {
                CollectIndices(value, rows);
            }

            if (columnLike)
            {
                CollectIndices(value, columns);
            }

            if (cellLike || (!rowLike && !columnLike))
            {
                CollectCells(value, cells);
            }
        }

        private static void CollectIndices(object value, List<int> destination)
        {
            if (TryConvertInt(value, out int single))
            {
                AddUnique(destination, single);
                return;
            }

            if (!(value is IEnumerable enumerable) || value is string)
            {
                return;
            }

            int boolIndex = 0;
            foreach (object item in enumerable)
            {
                if (item is bool flag)
                {
                    if (flag)
                    {
                        AddUnique(destination, boolIndex);
                    }

                    boolIndex++;
                    continue;
                }

                if (TryConvertInt(item, out int index))
                {
                    AddUnique(destination, index);
                }
            }
        }

        private static void CollectCells(object value, List<Vector2Int> destination)
        {
            if (TryConvertCell(value, out Vector2Int singleCell))
            {
                AddUnique(destination, singleCell);
                return;
            }

            if (!(value is IEnumerable enumerable) || value is string)
            {
                return;
            }

            foreach (object item in enumerable)
            {
                if (TryConvertCell(item, out Vector2Int cell))
                {
                    AddUnique(destination, cell);
                }
            }
        }

        private static bool TryConvertCell(object value, out Vector2Int cell)
        {
            cell = default;
            if (value == null)
            {
                return false;
            }

            if (value is Vector2Int vector2Int)
            {
                cell = vector2Int;
                return true;
            }

            if (value is Vector2 vector2)
            {
                cell = new Vector2Int(Mathf.RoundToInt(vector2.x), Mathf.RoundToInt(vector2.y));
                return true;
            }

            Type type = value.GetType();
            if (TryReadNamedInt(type, value, "x", out int x) && TryReadNamedInt(type, value, "y", out int y))
            {
                cell = new Vector2Int(x, y);
                return true;
            }

            if (TryReadNamedInt(type, value, "column", out int column) && TryReadNamedInt(type, value, "row", out int row))
            {
                cell = new Vector2Int(column, row);
                return true;
            }

            if (TryReadNamedInt(type, value, "Item1", out int item1) && TryReadNamedInt(type, value, "Item2", out int item2))
            {
                cell = new Vector2Int(item1, item2);
                return true;
            }

            return false;
        }

        private static bool TryReadNamedInt(Type type, object instance, string name, out int value)
        {
            value = 0;
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                try
                {
                    return TryConvertInt(field.GetValue(instance), out value);
                }
                catch
                {
                    return false;
                }
            }

            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return TryConvertInt(property.GetValue(instance, null), out value);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool TryConvertInt(object value, out int result)
        {
            result = 0;
            if (value == null || value is bool)
            {
                return false;
            }

            if (value is int intValue)
            {
                result = intValue;
                return true;
            }

            if (value is byte byteValue)
            {
                result = byteValue;
                return true;
            }

            if (value is short shortValue)
            {
                result = shortValue;
                return true;
            }

            if (value.GetType().IsEnum)
            {
                result = Convert.ToInt32(value);
                return true;
            }

            return false;
        }

        private static void DeriveLinesFromCells(List<Vector2Int> cells, List<int> rows, List<int> columns)
        {
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            int[] rowCounts = new int[BoardSize];
            int[] columnCounts = new int[BoardSize];
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                if (cell.y >= 0 && cell.y < BoardSize)
                {
                    rowCounts[cell.y]++;
                }

                if (cell.x >= 0 && cell.x < BoardSize)
                {
                    columnCounts[cell.x]++;
                }
            }

            for (int i = 0; i < BoardSize; i++)
            {
                if (rowCounts[i] >= BoardSize)
                {
                    AddUnique(rows, i);
                }

                if (columnCounts[i] >= BoardSize)
                {
                    AddUnique(columns, i);
                }
            }
        }

        private static void SanitizeIndices(List<int> indices)
        {
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                if (indices[i] < 0 || indices[i] >= BoardSize)
                {
                    indices.RemoveAt(i);
                }
            }

            indices.Sort();
        }

        private static void AddUnique(List<int> destination, int value)
        {
            if (!destination.Contains(value))
            {
                destination.Add(value);
            }
        }

        private static void AddUnique(List<Vector2Int> destination, Vector2Int value)
        {
            if (!destination.Contains(value))
            {
                destination.Add(value);
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            for (int i = 0; i < beams.Count; i++)
            {
                beams[i].routine = null;
                if (beams[i].root != null)
                {
                    beams[i].root.gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < particles.Count; i++)
            {
                particles[i].routine = null;
                if (particles[i].rect != null)
                {
                    particles[i].rect.gameObject.SetActive(false);
                }
            }
        }
    }
}
