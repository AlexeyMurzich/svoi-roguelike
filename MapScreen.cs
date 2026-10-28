using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        bool mapScreen;
        MapRun mapRun;
        Image mapBackground, mapIcons, ratEnemy;
        float mapScroll;
        int hoveredMapNode = -1;
        bool mapDragging, mapDragMoved, suppressMapClick;
        float dragStartY, dragStartScroll;
        readonly Dictionary<int, IntelNode> mapIntel = new Dictionary<int, IntelNode>();
        static readonly Random routeSeeds = new Random();
        void OpenActMap()
        {
            if (currentHero == null || currentHero.Effects.Chosen == null)
                return;
            mapScreen = true;
            if (mapRun == null)
            {
                mapRun = new MapRun(ActMap.Generate(routeSeeds.Next()));
                mapScroll = MapMaxScroll;
                mapIntel.Clear();
                var intel = new List<IntelNode>();
                foreach (var node in mapRun.Map.Nodes)
                {
                    var item = new IntelNode
                    {
                        Kind = node.Kind == RoomKind.Question ? IntelKind.Question : node.Kind == RoomKind.Elite ? IntelKind.Elite : node.Kind == RoomKind.Fight ? IntelKind.Fight : IntelKind.Other,
                        Identity = node.Kind == RoomKind.Question ? "Событие ещё не задано" : node.Kind == RoomKind.Fight ? OrdinaryEncounters.Preview(mapRun, node.Id) : node.Kind == RoomKind.Elite ? EnemyCatalog.Name(EnemyCatalog.ForEliteNode(mapRun.Map.Seed, node.Id)) : "Противник ещё не задан"
                    };
                    mapIntel.Add(node.Id, item);
                    intel.Add(item);
                }

                currentHero.Effects.Reveal(intel, new Random(mapRun.Map.Seed ^ 0x51ED));
            }

            checkout = false;
            mapScreen = true;
            SnapMap(mapScroll);
            previousMapMaxScroll = MapMaxScroll;
            preview = false;
            hoveredMapNode = -1;
            SetInventory("");
            mapDragging = false;
            suppressMapClick = false;
            RefreshScene();
        }

        void CompleteMapRoom()
        {
            if (HasLiveFight)
            {
                EnsureCombat();
                if (CombatBusy || combat.Phase != CombatPhase.Victory)
                    return;
            }

            if (mapRun.CompleteRoom())
            {
                foreach (var future in mapRun.Map.Nodes)
                    if (future.Kind == RoomKind.Fight && !mapRun.Visited(future.Id))
                        mapIntel[future.Id].Identity = OrdinaryEncounters.Preview(mapRun, future.Id);
                MoveMapTo(FocusMap(mapRun.Current));
            }
            ResetCombat();
            hoveredMapNode = -1;
        }

        PointF MapNodePoint(MapNode node)
        {
            var world = MapLayout.WorldPosition(node);
            return new PointF(CanvasWidth / 2 + (world.X - 640) * (CanvasWidth - 300) / 980, world.Y - mapScroll + MapView.Top);
        }

        void ScrollMap(float amount)
        {
            if (!mapScreen || inventoryView != "" || mapRun.Pending != null)
                return;
            MoveMapTo((MapMoving ? mapScrollTarget : mapScroll) + amount);
            hoveredMapNode = -1;
            RefreshScene();
        }

        void HandleMapKey(KeyEventArgs e)
        {
            if (inventoryView != "")
            {
                if (e.KeyCode == Keys.Escape || (e.KeyCode == Keys.D && inventoryView == "deck") || (e.KeyCode == Keys.R && inventoryView == "relics"))
                    SetInventory("");
                else if (e.KeyCode == Keys.Right && inventoryView == "deck")
                    deckPage = Math.Min(MaxDeckPage, deckPage + 1);
                else if (e.KeyCode == Keys.Left && inventoryView == "deck")
                    deckPage = Math.Max(0, deckPage - 1);
                return;
            }

            if (e.KeyCode == Keys.D)
            {
                SetInventory("deck");
                deckPage = 0;
                return;
            }

            if (e.KeyCode == Keys.R)
            {
                SetInventory("relics");
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                SetInventory("pause");
                return;
            }

            if (HasLiveFight)
            {
                EnsureCombat();
                HandleCombatKey(e);
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                if (mapRun.Pending != null)
                    CompleteMapRoom();
                else if (mapRun.Current.Exits.Count == 1)
                    EnterMapNode(mapRun.Current.Exits[0]);
            }
            else if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6)
            {
                int choice = e.KeyCode - Keys.D1;
                if (choice < mapRun.Current.Exits.Count)
                    EnterMapNode(mapRun.Current.Exits.OrderBy(id => mapRun.Map.Nodes[id].Lane).ElementAt(choice));
            }
            else if (e.KeyCode == Keys.Up)
                ScrollMap(-110);
            else if (e.KeyCode == Keys.Down)
                ScrollMap(110);
            else if (e.KeyCode == Keys.PageUp)
                ScrollMap(-440);
            else if (e.KeyCode == Keys.PageDown)
                ScrollMap(440);
            else if (e.KeyCode == Keys.Home)
                ScrollMap(-MapLayout.WorldHeight);
            else if (e.KeyCode == Keys.End)
                ScrollMap(MapLayout.WorldHeight);
            else if (e.KeyCode == Keys.Space)
            {
                MoveMapTo(FocusMap(mapRun.Current));
                hoveredMapNode = -1;
            }
        }

        void OnMapWheel(object sender, MouseEventArgs e)
        {
            if (!mapScreen || debugConsoleOpen)
                return;
            PointF point;
            if (DrawingViewport.TryMap(e.Location, out point) && MapView.Contains(point))
                ScrollMap(-e.Delta / 120f * 110);
        }

        int MapNodeAt(PointF point)
        {
            if (!MapView.Contains(point))
                return -1;
            foreach (var node in mapRun.Map.Nodes)
            {
                var center = MapNodePoint(node);
                float radius = node.Kind == RoomKind.Boss ? 38 : 26;
                if (Math.Abs(point.X - center.X) <= radius && Math.Abs(point.Y - center.Y) <= radius)
                    return node.Id;
            }

            return -1;
        }

        void OnMapMouseDown(object sender, MouseEventArgs e)
        {
            if (debugConsoleOpen) return;
            PointF routePoint;
            routePressed = e.Button == MouseButtons.Left && RouteButtonVisible && DrawingViewport.TryMap(e.Location, out routePoint) && RouteButtonArea.Contains(routePoint);
            if (routePressed) Invalidate();
            if (e.Button == MouseButtons.Left)
                suppressMapClick = false;
            if (!mapScreen || inventoryView != "" || mapRun.Pending != null || e.Button != MouseButtons.Left)
                return;
            PointF point;
            if (!DrawingViewport.TryMap(e.Location, out point) || !MapView.Contains(point) || RelicHitArea.Contains(point) || MapNodeAt(point) != -1 || MapLegendArea.Contains(point))
                return;
            StopMapMotion();
            mapDragging = true;
            mapDragMoved = false;
            dragStartY = point.Y;
            dragStartScroll = mapScroll;
            if (Visible)
                Capture = true;
        }

        void OnMapMouseMove(object sender, MouseEventArgs e)
        {
            PointF point;
            bool inside = DrawingViewport.TryMap(e.Location, out point);
            SetRelicHover(mapScreen && inside && inventoryView == "" && !mapDragging && RelicHitArea.Contains(point));
            UpdateCombatHover(point, inside);
            if (!mapScreen || inventoryView != "" || mapRun.Pending != null || !inside)
                return;
            if (mapDragging)
            {
                if (e.Button != MouseButtons.Left)
                {
                    CancelMapDrag();
                    return;
                }

                if (Math.Abs(point.Y - dragStartY) > 5)
                    mapDragMoved = true;
                if (mapDragMoved)
                {
                    MoveMapTo(dragStartScroll - point.Y + dragStartY);
                    RefreshScene();
                }

                return;
            }

            int id = hoveredRelic || MapLegendArea.Contains(point) ? -1 : MapNodeAt(point);
            if (id != hoveredMapNode)
            {
                hoveredMapNode = id;
                Invalidate();
            }
        }

        void OnMapMouseUp(object sender, MouseEventArgs e)
        {
            if (routePressed) { routePressed = false; Invalidate(); }
            if (e.Button != MouseButtons.Left || !mapDragging)
                return;
            CancelMapDrag();
        }

        void DrawMapConnection(Graphics g, MapNode from, MapNode to)
        {
            PointF a = MapNodePoint(from), b = MapNodePoint(to);
            if (Math.Max(a.Y, b.Y) < MapView.Top - 50 || Math.Min(a.Y, b.Y) > MapView.Bottom + 50)
                return;
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 1)
                return;
            float startGap = 28, endGap = to.Kind == RoomKind.Boss ? 40 : 28;
            a = new PointF(a.X + dx / length * startGap, a.Y + dy / length * startGap);
            b = new PointF(b.X - dx / length * endGap, b.Y - dy / length * endGap);
            bool traversed = mapRun.Visited(from.Id) && mapRun.Visited(to.Id);
            bool next = from.Id == mapRun.Current.Id && !mapRun.Finished;
            bool hover = from.Id == hoveredMapNode;
            Color color = traversed ? Color.FromArgb(166, 224, 168) : next || hover ? gold : Color.FromArgb(230, 239, 226, 198);
            var c1 = new PointF(a.X + dx * .08f, a.Y + (b.Y - a.Y) * .38f);
            var c2 = new PointF(b.X - dx * .08f, b.Y - (b.Y - a.Y) * .38f);
            float camera = mapScroll - MapView.Top;
            a.Y += camera; b.Y += camera; c1.Y += camera; c2.Y += camera;
            var path = mapRoutes.Get(mapRun.Map, CanvasWidth, from.Id * mapRun.Map.Nodes.Count + to.Id, a, b, c1, c2);
            var saved = g.Save();
            try
            {
                g.TranslateTransform(0, -camera);
                foreach (bool shadow in new[]
                {
                    true,
                    false
                }

                )
                    using (var pen = new Pen(shadow ? Color.FromArgb(220, 6, 14, 19) : color, shadow ? 5.5f : next || hover ? 3.4f : 2.8f))
                    {
                        if (!traversed)
                        {
                            pen.DashStyle = DashStyle.Custom;
                            pen.DashPattern = new[]
                            {
                                5f / pen.Width,
                                10f / pen.Width
                            };
                            pen.DashCap = DashCap.Round;
                        }

                        g.DrawPath(pen, path);
                    }
            }
            finally { g.Restore(saved); }
        }

        void DrawActMap(Graphics g)
        {
            if (HasLiveFight)
            {
                EnsureCombat();
                DrawCombat(g);
                return;
            }

            g.InterpolationMode = InterpolationMode.Bilinear;
            DrawSceneBackground(g, false);
            var saved = g.Save();
            try
            {
                g.SetClip(MapView);
                for (int district = 0; district < 3; district++)
                {
                    float worldY = 1900 - (district * 5 + 3) * 110;
                    float y = worldY - mapScroll + MapView.Top;
                    TextAt(g, ActMap.DistrictName(district), 32, y, 23, text, 186, true);
                    float boundary = 1900 - (district * 5 + 5.5f) * 110 - mapScroll + MapView.Top;
                    if (district < 2)
                    {
                        using (var pen = new Pen(Color.FromArgb(75, gold), 1))
                            g.DrawLine(pen, 28, boundary, CanvasWidth - 50, boundary);
                    }
                }

                foreach (var node in mapRun.Map.Nodes)
                    foreach (int next in node.Exits)
                        DrawMapConnection(g, node, mapRun.Map.Nodes[next]);
                var choices = mapRun.Current.Exits.OrderBy(id => mapRun.Map.Nodes[id].Lane).ToList();
                foreach (var node in mapRun.Map.Nodes)
                {
                    var point = MapNodePoint(node);
                    if (point.Y < MapView.Top - 50 || point.Y > MapView.Bottom + 50)
                        continue;
                    bool available = mapRun.CanEnter(node.Id), current = node.Id == mapRun.Current.Id;
                    float size = node.Kind == RoomKind.Boss ? 89 : 63;
                    float radius = node.Kind == RoomKind.Boss ? 43 : 30;
                    if (available || current || node.Id == hoveredMapNode)
                    {
                        using (var shade = new SolidBrush(Color.FromArgb(75, gold)))
                            g.FillEllipse(shade, point.X - radius, point.Y - radius, radius * 2, radius * 2);
                        using (var rim = new Pen(gold, available || current ? 2.5f : 1.3f))
                            g.DrawEllipse(rim, point.X - radius, point.Y - radius, radius * 2, radius * 2);
                    }

                    DrawRoomIcon(g, node.Kind, point, size);
                    if (mapRun.Visited(node.Id) && !current && node.Kind != RoomKind.Start)
                        using (var badge = new SolidBrush(Color.FromArgb(139, 219, 157)))
                            g.FillEllipse(badge, point.X + 17, point.Y + 15, 9, 9);
                    if (available)
                    {
                        TextAt(g, (choices.IndexOf(node.Id) + 1).ToString(), point.X + 33, point.Y - 12, 14, gold, 28, true);
                        int id = node.Id;
                        var hit = RectangleF.Intersect(MapView, new RectangleF(point.X - radius, point.Y - radius, radius * 2, radius * 2));
                        if (hit.Width > 0 && hit.Height > 0)
                            actions.Add(Tuple.Create(hit, (Action)(() => EnterMapNode(id))));
                    }
                    else if (current && mapRun.Pending == null && node.Kind != RoomKind.Start)
                    {
                        var hit = RectangleF.Intersect(MapView, new RectangleF(point.X - radius, point.Y - radius, radius * 2, radius * 2));
                        if (hit.Width > 0 && hit.Height > 0)
                            actions.Add(Tuple.Create(hit, (Action)OpenCurrentMapRoom));
                    }

                    if (mapIntel[node.Id].Revealed)
                        TextAt(g, "i", point.X - 37, point.Y - 12, 13, gold, 20, true);
                    if (node.Kind == RoomKind.Start)
                        ShadowText(g, "ЕВРООПТ · НАЧАЛО ПУТИ", point.X - 120, point.Y + 31, 12, text, 300, true);
                    if (node.Kind == RoomKind.Boss)
                        ShadowText(g, "ДОМ БОССА", point.X - 64, point.Y - 79, 18, gold, 270, true);
                }
            }
            finally
            {
                g.Restore(saved);
            }

            DrawRunBar(g);
            DrawMapRelics(g);
            DrawMapLegend(g);
            DrawInvestigationTooltip(g);
            if (mapRun.Pending != null)
                DrawMapRoom(g);
            if (inventoryView != "")
                DrawRunInventory(g);
            else if (hoveredRelic && !mapDragging)
                DrawRelicTooltip(g);
        }

        RectangleF MapLegendArea
        {
            get
            {
                return new RectangleF(CanvasWidth - 238, 188, 210, legendOpen ? 376 : 46);
            }
        }

        void DrawMapLegend(Graphics g)
        {
            var area = MapLegendArea;
            // The panel covers map rooms; its body must absorb clicks too.
            actions.Add(Tuple.Create(area, (Action)(() =>
            {
            })));
            Rect(g, area.X + 4, area.Y + 5, area.Width, area.Height, Color.FromArgb(125, 0, 0, 0));
            DrawInterfaceImage(g, hudTexture, area, Viewport.CoverSource(hudTexture.Size, area.Size));
            using (var shade = new LinearGradientBrush(area, Color.FromArgb(150, 9, 28, 24), Color.FromArgb(225, 7, 20, 20), 90f))
                g.FillRectangle(shade, area);
            Stroke(g, area.X, area.Y, area.Width, area.Height, Color.FromArgb(160, gold));
            Stroke(g, area.X + 4, area.Y + 4, area.Width - 8, area.Height - 8, Color.FromArgb(45, gold));
            using (var ornament = new SolidBrush(gold))
                g.FillPolygon(ornament, new[] { new PointF(area.X + 17, area.Y + 18), new PointF(area.X + 21, area.Y + 23), new PointF(area.X + 17, area.Y + 28), new PointF(area.X + 13, area.Y + 23) });
            TextAt(g, "Значки", area.X + 33, area.Y + 11, 17, gold, 135, true);
            using (var pen = new Pen(gold, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                float x = area.Right - 22, y = area.Y + 23;
                g.DrawLines(pen, legendOpen ? new[] { new PointF(x - 5, y - 2), new PointF(x, y + 3), new PointF(x + 5, y - 2) } : new[] { new PointF(x - 2, y - 5), new PointF(x + 3, y), new PointF(x - 2, y + 5) });
            }

            actions.Add(Tuple.Create(new RectangleF(area.X, area.Y, area.Width, 46), (Action)(() => legendOpen = !legendOpen)));
            if (!legendOpen)
                return;
            using (var line = new Pen(Color.FromArgb(95, gold)))
                g.DrawLine(line, area.X + 12, area.Y + 46, area.Right - 12, area.Y + 46);
            var kinds = new[]
            {
                RoomKind.Fight,
                RoomKind.Question,
                RoomKind.Rest,
                RoomKind.Shop,
                RoomKind.Elite,
                RoomKind.Boss
            };
            for (int i = 0; i < kinds.Length; i++)
            {
                float y = area.Y + 54 + i * 51;
                Rect(g, area.X + 10, y, area.Width - 20, 47, Color.FromArgb(i % 2 == 0 ? 65 : 30, 45, 69, 55));
                DrawRoomIcon(g, kinds[i], new PointF(area.X + 37, y + 23), 44);
                TextAt(g, kinds[i] == RoomKind.Elite ? "Элитный враг" : kinds[i] == RoomKind.Boss ? "Босс" : RoomName(kinds[i]), area.X + 68, y + 13, 14, text, area.Width - 77);
            }
        }

        void DrawMapRoom(Graphics g)
        {
            actions.Clear();
            var node = mapRun.Pending;
            Rect(g, MapView.X, MapView.Y, MapView.Width, MapView.Height, Color.FromArgb(220, 7, 13, 18));
            DrawRunBar(g);
            DrawMapRelics(g);
            DrawCenteredOverlay(g, DrawMapRoomContent);
        }

        void DrawMapRoomContent(Graphics g)
        {
            var node = mapRun.Pending;
            RelicFrame(g, 323, 211, 634, 416, true);
            TextAt(g, ActMap.DistrictName(node.District), 355, 241, 12, gold, 560, true);
            if (node.Kind == RoomKind.Elite && mapRun.PendingIsRevisit)
            {
                TextAt(g, "Мимикратор побеждена", 355, 281, 30, text, 560, true);
                Button(g, "Вернуться на карту", 355, 540, 570, 53, CompleteMapRoom, true);
                return;
            }
            if (node.Kind == RoomKind.Fight)
            {
                var enemy = EnemyAtNode(node.Id);
                TextAt(g, EnemyCatalog.Name(enemy), 355, 281, 32, text, 560, true);
                Portrait(g, selected, 365, 324, 180, 205);
                if (enemy == EnemyKind.StrayDogs)
                {
                    DrawDogPoseFor(g, new RectangleF(555, 324, 180, 205), 0);
                    DrawDogPoseFor(g, new RectangleF(750, 324, 180, 205), 1);
                }
                else if (enemy == EnemyKind.Hedgehogs)
                {
                    DrawHedgehogPose(g, new RectangleF(550, 324, 185, 205), false, false);
                    DrawHedgehogPose(g, new RectangleF(750, 324, 185, 205), true, false);
                }
                else if (enemy == EnemyKind.Babai) { LoadBabaiArt(); FitImage(g, babaiBed, new RectangleF(575, 324, 350, 205)); }
                else if (enemy == EnemyKind.Rat) FitImage(g, ratEnemy, new RectangleF(575, 324, 350, 205));
                else DrawHedgehogPose(g, new RectangleF(575, 324, 350, 205), enemy == EnemyKind.RabidHedgehog, false);
                Button(g, mapRun.PendingIsRevisit ? "Вернуться на карту" : "Продолжить тестовый маршрут  →", 355, 540, 570, 53, CompleteMapRoom, true);
                return;
            }

            TextAt(g, RoomName(node.Kind), 355, 281, 32, RoomColor(node.Kind), 560, true);
            string detail = node.Kind == RoomKind.Boss ? "Двухэтажный дом из голых бетонных блоков, без забора. Здесь будет босс Смолевичей." : node.Kind == RoomKind.Rest ? "Здесь будет отдых у костра." : node.Kind == RoomKind.Shop ? "Здесь будет магазин карт." : node.Kind == RoomKind.Question ? "Здесь будет случайное событие." : "Здесь будет " + (node.Kind == RoomKind.Elite ? "элитный враг." : "обычный бой.");
            TextAt(g, detail, 355, 346, 18, text, 560);
            TextAt(g, "Содержимое этой комнаты и награды появятся позже.", 355, 425, 14, muted, 560);
            if (mapIntel[node.Id].Revealed)
                TextAt(g, "Расследование: " + mapIntel[node.Id].Identity, 355, 483, 12, gold, 560);
            Button(g, mapRun.PendingIsRevisit ? "Вернуться на карту" : "Продолжить тестовый маршрут  →", 355, 540, 570, 53, CompleteMapRoom, true);
        }
    }
}
