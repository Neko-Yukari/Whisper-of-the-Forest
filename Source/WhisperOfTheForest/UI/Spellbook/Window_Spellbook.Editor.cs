using RimWorld;
using UnityEngine;
using Verse;
using WhisperOfTheForest.Experimental.Spells;

namespace WhisperOfTheForest.Experimental
{
    /// <summary>
    /// Right page: the spell name, the spell bar with its cast timing, the description of the
    /// hovered rune, the live validation, and the create / clear-bar / delete-slot / cast actions.
    ///
    /// One click appends a glyph from the catalogue and one click on a bead removes it again, so the
    /// bar needs no selection, no reorder tools and no confirmation dialogs.
    /// </summary>
    public partial class Window_Spellbook
    {
        /// <summary>
        /// The bar shows a socket for every rune the tuning allows, so its height does not jump as
        /// runes are added; a bar loaded with more (an old save) still gets one socket per rune.
        /// </summary>
        private EditorLayout EditorLayoutFor(Rect page)
        {
            return EditorLayout.Compute(page, Mathf.Max(SpellbookStyle.MaxGlyphs, chain.glyphs.Count));
        }

        private void DrawEditorPage(Rect page)
        {
            SpellbookStyle.DrawPage(page, false);
            EditorLayout layout = EditorLayoutFor(page);

            SpellbookStyle.DrawPageTitle(layout.Title, layout.Divider, T("WotF_Spellbook.PageEditor"));
            SpellbookStyle.DrawLabel(layout.NameCaption, T("WotF_Spellbook.FieldName"), GameFont.Tiny,
                SpellbookStyle.InkDim, TextAnchor.MiddleLeft);
            chain.name = SpellbookStyle.TextField(layout.NameField, chain.name, null, null);

            DrawChainCaption(layout.ChainCaption);
            DrawBar(layout);
            DrawGlyphInfo(layout.Detail);
            DrawValidation(layout.Validation);
            DrawActionRow(layout.ActionRow);
            DrawCastRow(layout.CastRow);
        }

        // ---------------------------------------------------------------- spell bar

        private SpellGlyphDef GlyphDefAt(int index)
        {
            if (index < 0 || index >= chain.glyphs.Count)
            {
                return null;
            }

            string defName = chain.glyphs[index];
            return defName.NullOrEmpty() ? null : SpellDefs.Glyph(defName);
        }

        /// <summary>"Spell bar 3/10" on the left; on the right the windup / channel / recovery time
        /// that "create" would freeze into the slot.</summary>
        private void DrawChainCaption(Rect rect)
        {
            string count = TF("WotF_Spellbook.ChainHeader", chain.glyphs.Count, SpellbookStyle.MaxGlyphs);
            float countWidth = SpellbookStyle.TextWidth(count, GameFont.Tiny) + 8f;
            SpellbookStyle.DrawLabel(new Rect(rect.x, rect.y, countWidth, rect.height), count, GameFont.Tiny,
                SpellbookStyle.InkDim, TextAnchor.MiddleLeft);

            if (!chain.HasGlyphs)
            {
                return;
            }

            string timing = TF("WotF_Spellbook.Timing", SpellbookStyle.Seconds(barTiming.windupTicks),
                SpellbookStyle.Seconds(barTiming.channelTicks), SpellbookStyle.Seconds(barTiming.recoveryTicks));
            float timingWidth = Mathf.Min(SpellbookStyle.TextWidth(timing, GameFont.Tiny) + 4f,
                rect.width - countWidth - 18f);
            if (timingWidth <= 20f)
            {
                return;
            }

            Rect timingRect = new Rect(rect.xMax - timingWidth, rect.y, timingWidth, rect.height);
            const float iconSize = 12f;
            SpellbookStyle.DrawIcon(new Rect(timingRect.x - iconSize - 4f, rect.y + (rect.height - iconSize) * 0.5f,
                iconSize, iconSize), SpellbookStyle.IconHourglass, SpellbookStyle.InkDim);
            SpellbookStyle.DrawLabel(timingRect, timing, GameFont.Tiny, SpellbookStyle.InkDim, TextAnchor.MiddleRight);
            TooltipHandler.TipRegion(new Rect(timingRect.x - iconSize - 4f, rect.y, timingWidth + iconSize + 4f,
                rect.height), TF("WotF_Spellbook.TimingTip", barTiming.windupTicks, barTiming.channelTicks,
                barTiming.recoveryTicks));
        }

        private void DrawBar(EditorLayout layout)
        {
            hoverGlyph = -1;

            // A vine runs through every socket row.
            for (int row = 0; row < layout.BeadRows; row++)
            {
                float centerY = layout.Chain.y + row * (SpellbookLayout.BeadHeight + SpellbookLayout.BeadRowGap)
                                + SpellbookLayout.SocketSize * 0.5f;
                SpellbookStyle.DrawStretched(new Rect(layout.Chain.x - 8f, centerY - 10f, layout.Chain.width + 16f, 20f),
                    SpellbookStyle.VineTrack, new Color(1f, 1f, 1f, 0.85f));
            }

            TooltipHandler.TipRegion(layout.Chain, T("WotF_Spellbook.ChainHint"));

            // The shake is a drawing offset only: the layout keeps its rectangles, and the same
            // offset is applied to the click rectangle so a click cannot land on a neighbour.
            float shake = 0f;
            if (anim.TryShake(out float shakeProgress))
            {
                shake = Mathf.Sin(shakeProgress * 34f) * 3.5f * (1f - shakeProgress);
            }

            int sockets = layout.BeadColumns * layout.BeadRows;
            int capacity = Mathf.Max(SpellbookStyle.MaxGlyphs, chain.glyphs.Count);
            for (int i = 0; i < sockets && i < capacity; i++)
            {
                Rect bead = layout.BeadRect(i);
                if (i >= chain.glyphs.Count)
                {
                    DrawEmptySocket(bead, i, i == chain.glyphs.Count);
                    continue;
                }

                bead.x += shake;
                if (DrawBead(bead, i))
                {
                    // The list changed size; the rest is redrawn next frame.
                    break;
                }
            }

            if (anim.TryShake(out float flashProgress))
            {
                Widgets.DrawBoxSolid(layout.Chain,
                    new Color(SpellbookStyle.Warning.r, SpellbookStyle.Warning.g, SpellbookStyle.Warning.b,
                        0.20f * (1f - flashProgress)));
            }
        }

        /// <summary>Draws one rune bead; returns true when a click removed it from the bar.</summary>
        private bool DrawBead(Rect bead, int index)
        {
            SpellGlyphDef glyph = GlyphDefAt(index);
            bool hovered = Mouse.IsOver(bead);
            Rect socket = EditorLayout.SocketRect(bead);

            if (hovered)
            {
                Color glowColor = glyph != null ? SpellbookStyle.KindColor(glyph.kind) : SpellbookStyle.Warning;
                SpellbookStyle.DrawGlow(socket.center, socket.width * 1.7f, glowColor, 0.6f);
            }

            Texture2D icon = SpellbookStyle.GlyphIcon(glyph);
            SpellbookStyle.DrawSocket(socket, icon, 1f);
            if (icon == null)
            {
                SpellbookStyle.DrawLabel(socket, "?", GameFont.Small, SpellbookStyle.CoverWarning,
                    TextAnchor.MiddleCenter);
            }

            SpellbookStyle.DrawLabel(new Rect(bead.x, socket.yMax, bead.width, bead.yMax - socket.yMax),
                (index + 1).ToString(), GameFont.Tiny, SpellbookStyle.InkDim, TextAnchor.MiddleCenter);

            if (hovered)
            {
                hoverGlyph = index;
                // Click-to-remove affordance in the socket's corner.
                SpellbookStyle.DrawIcon(new Rect(socket.xMax - 13f, socket.y - 2f, 15f, 15f), SpellbookStyle.IconRemove,
                    SpellbookStyle.Rust);
            }

            TooltipHandler.TipRegion(bead, TF("WotF_Spellbook.ClickToRemove",
                glyph != null ? glyph.LabelCap : T("WotF_Spellbook.GlyphMissing")));

            if (Widgets.ButtonInvisible(bead, true))
            {
                RemoveGlyph(index, bead);
                return true;
            }

            return false;
        }

        /// <summary>A socket the bar has room for; the next one to fill is lit faintly.</summary>
        private static void DrawEmptySocket(Rect bead, int index, bool isNext)
        {
            Rect socket = EditorLayout.SocketRect(bead);
            if (isNext)
            {
                SpellbookStyle.DrawGlow(socket.center, socket.width * 1.6f, SpellbookStyle.JadeBright, 0.3f);
            }

            SpellbookStyle.DrawSocket(socket, null, isNext ? 0.75f : 0.4f);
            SpellbookStyle.DrawLabel(new Rect(bead.x, socket.yMax, bead.width, bead.yMax - socket.yMax),
                (index + 1).ToString(), GameFont.Tiny, SpellbookStyle.InkFaint, TextAnchor.MiddleCenter);
        }

        private void RemoveGlyph(int index, Rect bead)
        {
            string defName = index >= 0 && index < chain.glyphs.Count ? chain.glyphs[index] : null;
            chain.RemoveAt(index);
            if (hoverGlyph >= chain.glyphs.Count)
            {
                hoverGlyph = -1;
            }

            anim.OnRemove(bead, defName);
            SpellGlyphDef glyph = defName.NullOrEmpty() ? null : SpellDefs.Glyph(defName);
            SetStatus(TF("WotF_Spellbook.GlyphRemoved", glyph != null ? glyph.LabelCap : defName ?? ""), false);
        }

        // ---------------------------------------------------------------- detail panels

        /// <summary>
        /// The rune under the mouse, in the bar or in the catalogue: icon, name, kind, the time it
        /// adds to each phase and its description.
        /// </summary>
        private void DrawGlyphInfo(Rect rect)
        {
            if (rect.height < 28f)
            {
                return;
            }

            SpellbookStyle.DrawPanel(rect, SpellbookStyle.Fade(SpellbookStyle.Ink, 0.40f));
            Rect inner = rect.ContractedBy(10f);

            SpellGlyphDef glyph = GlyphDefAt(hoverGlyph) ?? catalogHover;
            if (glyph == null)
            {
                SpellbookStyle.DrawLabel(new Rect(inner.x, inner.y, inner.width, 16f), T("WotF_Spellbook.GlyphDetail"),
                    GameFont.Tiny, SpellbookStyle.InkFaint);
                SpellbookStyle.DrawParagraph(new Rect(inner.x, inner.y + 18f, inner.width, Mathf.Max(0f, inner.height - 18f)),
                    chain.HasGlyphs ? T("WotF_Spellbook.NoSelection") : T("WotF_Spellbook.ChainEmpty"),
                    GameFont.Small, SpellbookStyle.InkDim);
                return;
            }

            const float socketSize = 40f;
            Rect socket = new Rect(inner.x, inner.y, socketSize, socketSize);
            SpellbookStyle.DrawSocket(socket, SpellbookStyle.GlyphIcon(glyph), 1f);

            float textX = socket.xMax + 8f;
            float textWidth = inner.xMax - textX;
            SpellbookStyle.DrawLabel(new Rect(textX, inner.y, textWidth, 22f), glyph.LabelCap, GameFont.Small,
                SpellbookStyle.Ink);
            const float tagWidth = 72f;
            SpellbookStyle.DrawKindTag(new Rect(textX, inner.y + 22f, tagWidth, 16f), glyph.kind, 1f);
            if (glyph.windupTicks > 0 || glyph.channelTicks > 0 || glyph.recoveryTicks > 0)
            {
                SpellbookStyle.DrawLabel(new Rect(textX + tagWidth, inner.y + 22f, textWidth - tagWidth, 16f),
                    TF("WotF_Spellbook.GlyphTiming", SpellbookStyle.Seconds(glyph.windupTicks),
                        SpellbookStyle.Seconds(glyph.channelTicks), SpellbookStyle.Seconds(glyph.recoveryTicks)),
                    GameFont.Tiny, SpellbookStyle.InkDim, TextAnchor.MiddleLeft);
            }

            float bodyTop = socket.yMax + 6f;
            SpellbookStyle.DrawParagraph(new Rect(inner.x, bodyTop, inner.width, Mathf.Max(0f, inner.yMax - bodyTop)),
                glyph.description ?? "", GameFont.Small, SpellbookStyle.InkDim);
        }

        private void DrawValidation(Rect rect)
        {
            Color color = chainValid ? SpellbookStyle.Good : SpellbookStyle.Warning;
            SpellbookStyle.DrawPanel(rect, color);

            const float iconSize = 18f;
            SpellbookStyle.DrawIcon(new Rect(rect.x + 10f, rect.y + 7f, iconSize, iconSize),
                chainValid ? SpellbookStyle.IconOk : SpellbookStyle.IconWarn, color);

            float textX = rect.x + 10f + iconSize + 8f;
            float textWidth = rect.xMax - 8f - textX;
            string firstLine = chainValid
                ? T("WotF_Spellbook.ValidationOk")
                : TF("WotF_Spellbook.ValidationFailed", ReasonOrUnknown(chainReason));
            SpellbookStyle.DrawLabel(new Rect(textX, rect.y + 5f, textWidth, 20f), firstLine, GameFont.Small, color);

            string secondLine;
            Color secondColor;
            if (slotNames[slot].NullOrEmpty())
            {
                // The active slot is empty, so the pawn's cast gizmo has nothing to fire.
                secondLine = T("WotF_Spellbook.EmptySlotHint");
                secondColor = SpellbookStyle.InkDim;
            }
            else if (!castable)
            {
                secondLine = TF("WotF_Spellbook.CastBlocked", ReasonOrUnknown(castReason));
                secondColor = SpellbookStyle.Warning;
            }
            else
            {
                secondLine = TF("WotF_Spellbook.CastReady", slot + 1);
                secondColor = SpellbookStyle.Good;
            }

            SpellbookStyle.DrawParagraph(new Rect(textX, rect.y + 25f, textWidth, Mathf.Max(0f, rect.height - 27f)),
                secondLine, GameFont.Tiny, secondColor);
        }

        // ---------------------------------------------------------------- action rows

        private void DrawActionRow(Rect rect)
        {
            const float gap = 6f;
            float width = (rect.width - 2f * gap) / 3f;
            Rect createRect = new Rect(rect.x, rect.y, width, rect.height);
            Rect clearRect = new Rect(createRect.xMax + gap, rect.y, width, rect.height);
            Rect deleteRect = new Rect(clearRect.xMax + gap, rect.y, rect.xMax - clearRect.xMax - gap, rect.height);

            // One gate, from the shared validator: an invalid bar cannot be created, and the reason
            // is already on screen right above this row.
            if (SpellbookStyle.Button(createRect, TF("WotF_Spellbook.Create", slot + 1), SpellbookButtonRole.Primary,
                    chainValid))
            {
                RequestCreate();
            }

            TooltipHandler.TipRegion(createRect, TF("WotF_Spellbook.CreateTip", slot + 1));

            if (SpellbookStyle.Button(clearRect, T("WotF_Spellbook.ClearBar"), SpellbookButtonRole.Secondary,
                    chain.HasGlyphs))
            {
                RequestClearBar();
            }

            TooltipHandler.TipRegion(clearRect, T("WotF_Spellbook.ClearBarTip"));

            if (SpellbookStyle.Button(deleteRect, T("WotF_Spellbook.DeleteSlot"), SpellbookButtonRole.Danger,
                    !slotNames[slot].NullOrEmpty()))
            {
                RequestDeleteSlot();
            }

            TooltipHandler.TipRegion(deleteRect, TF("WotF_Spellbook.DeleteSlotTip", slot + 1));
        }

        private void DrawCastRow(Rect rect)
        {
            if (castable)
            {
                SpellbookStyle.DrawStretched(rect.ExpandedBy(10f, 8f), SpellbookStyle.Glow,
                    SpellbookStyle.Fade(SpellbookStyle.JadeBright, 0.35f));
            }

            string castLabel = slotNames[slot].NullOrEmpty()
                ? T("WotF_Spellbook.Cast")
                : TF("WotF_Spellbook.CastNamed", slotNames[slot]);
            if (SpellbookStyle.Button(rect, castLabel, SpellbookButtonRole.Primary, castable))
            {
                RequestCast();
            }

            TooltipHandler.TipRegion(rect, castable
                ? TF("WotF_Spellbook.CastTip", slot + 1)
                : (castReason.NullOrEmpty() ? T("WotF_Spellbook.EmptySlotHint") : castReason));
        }

        // ---------------------------------------------------------------- animations

        /// <summary>
        /// All feedback is an overlay: it is drawn after the page and never feeds back into the
        /// layout rectangles, so a disabled animation leaves the page exactly as it was.
        /// </summary>
        private void DrawAnimations(SpellbookLayout layout)
        {
            EditorLayout editor = EditorLayoutFor(layout.RightPage);

            DrawAddedGlyph(editor);
            DrawRemovedGlyph();
            DrawSweepingLight(editor.Chain);
            DrawFlipBand(layout);
        }

        private void DrawAddedGlyph(EditorLayout editor)
        {
            if (!anim.TryAdd(out float progress, out Vector2 from, out int index))
            {
                return;
            }

            Vector2 to = EditorLayout.SocketRect(editor.BeadRect(index)).center;
            float eased = SpellbookAnim.EaseOut(progress);
            Vector2 position = new Vector2(from.x + (to.x - from.x) * eased, from.y + (to.y - from.y) * eased);
            float scale = 1.45f - 0.45f * eased;
            float alpha = progress < 0.75f ? 1f : (1f - progress) / 0.25f;
            float size = SpellbookLayout.SocketSize * scale;

            Texture2D icon = SpellbookStyle.GlyphIcon(GlyphDefAt(index));
            if (icon == null)
            {
                return;
            }

            SpellbookStyle.DrawGlow(position, size * 1.6f, SpellbookStyle.JadeBright, 0.5f * alpha);
            SpellbookStyle.DrawIcon(new Rect(position.x - size * 0.5f, position.y - size * 0.5f, size, size), icon,
                new Color(1f, 1f, 1f, alpha));
        }

        private void DrawRemovedGlyph()
        {
            if (!anim.TryRemove(out float progress, out Rect bead, out string defName))
            {
                return;
            }

            Rect socket = EditorLayout.SocketRect(bead);
            float size = socket.width * (1f - 0.45f * progress);
            Rect iconRect = new Rect(socket.center.x - size * 0.5f, socket.center.y - size * 0.5f, size, size);

            Texture2D icon = SpellbookStyle.GlyphIcon(defName.NullOrEmpty() ? null : SpellDefs.Glyph(defName));
            if (icon != null)
            {
                SpellbookStyle.DrawIcon(iconRect, icon, new Color(1f, 1f, 1f, 1f - progress));
            }
        }

        private void DrawSweepingLight(Rect bar)
        {
            if (!anim.TrySweep(out float progress))
            {
                return;
            }

            const float bandWidth = 56f;
            float x = bar.x - bandWidth + (bar.width + bandWidth) * progress;
            DrawSoftBand(new Rect(x, bar.y - 2f, bandWidth, bar.height + 4f), SpellbookStyle.JadeBright,
                0.55f * SpellbookAnim.Pulse(progress));
        }

        private void DrawFlipBand(SpellbookLayout layout)
        {
            if (!anim.TryFlip(out float progress, out int direction))
            {
                return;
            }

            Rect area = new Rect(layout.LeftPage.x, layout.LeftPage.y,
                layout.RightPage.xMax - layout.LeftPage.x, layout.LeftPage.height);
            const float bandWidth = 64f;
            float x = direction >= 0
                ? area.x - bandWidth + (area.width + bandWidth) * progress
                : area.xMax - area.width * progress;
            DrawSoftBand(new Rect(x, area.y, bandWidth, area.height), SpellbookStyle.CoverText,
                0.30f * SpellbookAnim.Pulse(progress));
        }

        /// <summary>Flat slices fading towards both ends; cheaper and steadier than a gradient texture.</summary>
        private static void DrawSoftBand(Rect rect, Color color, float alpha)
        {
            if (alpha <= 0f || rect.width <= 0f)
            {
                return;
            }

            const int slices = 6;
            float sliceWidth = rect.width / slices;
            for (int i = 0; i < slices; i++)
            {
                float center = (i + 0.5f) / slices;
                float weight = 1f - Mathf.Abs(center * 2f - 1f);
                Widgets.DrawBoxSolid(new Rect(rect.x + i * sliceWidth, rect.y, sliceWidth, rect.height),
                    new Color(color.r, color.g, color.b, alpha * weight));
            }
        }
    }
}
