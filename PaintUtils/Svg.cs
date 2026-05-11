using System.Xml.Linq;

namespace PaintUtils
{
    public class SvgGenerator
    {
        private readonly XNamespace svgNs = "http://www.w3.org/2000/svg";
        private readonly XElement svgElement;
        private readonly List<XElement> elements = new List<XElement>();
        private readonly Dictionary<string, XElement> markers = new Dictionary<string, XElement>(); // 存储箭头标记
        public readonly double width;
        public readonly double height;

        // 构造函数：初始化 SVG 画布
        public SvgGenerator(double width, double height, string viewBox = null, string backgroundColor = null)
        {
            this.width = width;
            this.height = height;
            svgElement = new XElement(svgNs + "svg",
                new XAttribute("xmlns", svgNs),
                new XAttribute("width", width),
                new XAttribute("height", height)
            );

            if (!string.IsNullOrEmpty(viewBox))
            {
                svgElement.Add(new XAttribute("viewBox", viewBox));
            }

            if (!string.IsNullOrEmpty(backgroundColor))
            {
                AddRectangle(0, 0, width, height, backgroundColor);
            }
        }

        // 添加线段
        public SvgGenerator AddLine(double x1, double y1, double x2, double y2, string strokeColor, double strokeWidth = 2)
        {
            elements.Add(new XElement(svgNs + "line",
                new XAttribute("x1", x1),
                new XAttribute("y1", y1),
                new XAttribute("x2", x2),
                new XAttribute("y2", y2),
                new XAttribute("stroke", strokeColor),
                new XAttribute("stroke-width", strokeWidth)
            ));
            return this;
        }
        // 添加实心圆
        public SvgGenerator AddCircle(double cx, double cy, double radius, string fill)
        {
            elements.Add(new XElement(svgNs + "circle",
                new XAttribute("cx", cx),
                new XAttribute("cy", cy),
                new XAttribute("r", radius),
                new XAttribute("fill", fill)
            ));
            return this;
        }
        // 添加文本
        public SvgGenerator AddText(string text, double x, double y, string fill = "black", string fontFamily = "Arial", int fontSize = 12)
        {
            elements.Add(new XElement(svgNs + "text",
                new XAttribute("x", x),
                new XAttribute("y", y),
                new XAttribute("fill", fill),
                new XAttribute("font-family", fontFamily),
                new XAttribute("font-size", fontSize),
                text
            ));
            return this;
        }
        //添加区域文本,如果区域不是矩形，会进行旋转
        public SvgGenerator AddText(
            string text,
            double x1, double y1, // 区域起点
            double x2, double y2, // 区域终点
            string fill = "black",
            string fontFamily = "Arial",
            int fontSize = 12)
        {
            // 计算区域中心点
            double centerX = (x1 + x2) / 2;
            double centerY = (y1 + y2) / 2;

            // 计算旋转角度（从起点到终点的方向）
            double angleRad = Math.Atan2(y2 - y1, x2 - x1);
            double angleDeg = angleRad * (180 / Math.PI);

            // 添加文本元素，应用旋转和居中
            elements.Add(new XElement(svgNs + "text",
                new XAttribute("x", centerX),
                new XAttribute("y", centerY),
                new XAttribute("fill", fill),
                new XAttribute("font-family", fontFamily),
                new XAttribute("font-size", fontSize),
                new XAttribute("text-anchor", "middle"),      // 水平居中
                new XAttribute("dominant-baseline", "middle"),// 垂直居中
                new XAttribute("transform", $"rotate({angleDeg}, {centerX}, {centerY})"),
                text
            ));

            return this;
        }
        // 添加矩形（用于背景等）
        public SvgGenerator AddRectangle(double x, double y, double width, double height, string fill)
        {
            elements.Add(new XElement(svgNs + "rect",
                new XAttribute("x", x),
                new XAttribute("y", y),
                new XAttribute("width", width),
                new XAttribute("height", height),
                new XAttribute("fill", fill)
            ));
            return this;
        }
        // 添加圆角矩形
        public SvgGenerator AddRoundedRectangle(
            double x, double y, double width, double height,
            double cornerRadiusX, double cornerRadiusY,
            string fill, string stroke = null, double strokeWidth = 1)
        {
            var rect = new XElement(svgNs + "rect",
                new XAttribute("x", x),
                new XAttribute("y", y),
                new XAttribute("width", width),
                new XAttribute("height", height),
                new XAttribute("rx", cornerRadiusX),
                new XAttribute("ry", cornerRadiusY),
                new XAttribute("fill", fill)
            );

            if (!string.IsNullOrEmpty(stroke))
            {
                rect.Add(new XAttribute("stroke", stroke),
                         new XAttribute("stroke-width", strokeWidth));
            }

            elements.Add(rect);
            return this;
        }

        // 添加箭头标记（定义在 defs 中）
        private void AddArrowMarker(string id, string color, double size = 10)
        {
            if (markers.ContainsKey(id)) return;

            var marker = new XElement(svgNs + "marker",
                new XAttribute("id", id),
                new XAttribute("markerWidth", size),
                new XAttribute("markerHeight", size),
                new XAttribute("refX", size / 2),
                new XAttribute("refY", size / 2),
                new XAttribute("orient", "auto"),
                new XElement(svgNs + "path",
                    new XAttribute("d", $"M0,0 L{size},{size / 2} L0,{size} L{size / 2},{size / 2} L0,0"),
                    new XAttribute("fill", color)
                )
            );

            markers.Add(id, marker);
        }
        // 添加带箭头的线段
        public SvgGenerator AddArrowLine(
            double x1, double y1, double x2, double y2,
            string strokeColor, double strokeWidth = 2,
            string arrowType = "triangle", double arrowSize = 10)
        {
            // 定义箭头标记
            string markerId = $"{arrowType}-{strokeColor}-{arrowSize}";
            AddArrowMarker(markerId, strokeColor, arrowSize);

            // 添加线段并引用箭头
            elements.Add(new XElement(svgNs + "line",
                new XAttribute("x1", x1),
                new XAttribute("y1", y1),
                new XAttribute("x2", x2),
                new XAttribute("y2", y2),
                new XAttribute("stroke", strokeColor),
                new XAttribute("stroke-width", strokeWidth),
                new XAttribute("marker-end", $"url(#{markerId})") // 箭头在终点
            ));

            return this;
        }
        public SvgGenerator AddPath(
            string pathData,
            string strokeColor, double strokeWidth = 2,
            string fill = "none", bool addArrow = false)
        {
            if (addArrow)
            {
                string markerId = $"arrow-{strokeColor}-{strokeWidth}";
                AddArrowMarker(markerId, strokeColor);
                elements.Add(new XElement(svgNs + "path",
                    new XAttribute("d", pathData),
                    new XAttribute("stroke", strokeColor),
                    new XAttribute("stroke-width", strokeWidth),
                    new XAttribute("fill", fill),
                    new XAttribute("marker-end", $"url(#{markerId})")
                ));
            }
            else
            {
                elements.Add(new XElement(svgNs + "path",
                    new XAttribute("d", pathData),
                    new XAttribute("stroke", strokeColor),
                    new XAttribute("stroke-width", strokeWidth),
                    new XAttribute("fill", fill)
                ));
            }

            return this;
        }
        // 添加子 SVG 到指定位置
        public SvgGenerator AddSubSvg(SvgGenerator subSvg, double x, double y, float scale = 1.0f)
        {
            // 创建 <g> 分组并应用变换
            var group = new XElement(svgNs + "g",
                new XAttribute("transform", $"translate({x},{y}) scale({scale})")
            );

            // 克隆子 SVG 的元素（避免 XObject 重复添加问题）
            foreach (var element in subSvg.elements)
            {
                group.Add(new XElement(element));
            }

            // 合并子 SVG 的 markers 并处理 ID 冲突
            foreach (var marker in subSvg.markers)
            {
                string originalId = marker.Key;
                if (markers.ContainsKey(originalId))
                {
                    // 生成唯一 ID（添加随机后缀）
                    string newId = $"{originalId}-{Guid.NewGuid().ToString("N")}";
                    XElement newMarker = new XElement(marker.Value);
                    newMarker.SetAttributeValue("id", newId);

                    // 更新子元素的 marker 引用
                    UpdateMarkerReferences(group, originalId, newId);
                    markers.Add(newId, newMarker);
                }
                else
                {
                    markers.Add(originalId, new XElement(marker.Value));
                }
            }

            elements.Add(group);
            return this;
        }

        private void UpdateMarkerReferences(XElement element, string oldId, string newId)
        {
            foreach (var attr in element.Attributes())
            {
                if (attr.Name == "marker-end" || attr.Name == "marker-start")
                {
                    attr.Value = attr.Value.Replace(oldId, newId);
                }
            }

            foreach (var child in element.Elements())
            {
                UpdateMarkerReferences(child, oldId, newId);
            }
        }
        public SvgGenerator MoveToView()//移动到视口里，似乎有bug
        {
            double minX = 0;
            double minY = 0;

            foreach (var element in elements)
            {
                // 提取所有可能的坐标属性
                var xAttributes = element.Attributes().Where(a => a.Name.LocalName.StartsWith("x"));
                var yAttributes = element.Attributes().Where(a => a.Name.LocalName.StartsWith("y"));

                // 更新最小 x 和 y 值
                foreach (var xAttr in xAttributes)
                {
                    if (double.TryParse(xAttr.Value, out double x))
                    {
                        if (x < minX)
                            minX = x;
                    }
                }

                foreach (var yAttr in yAttributes)
                {
                    if (double.TryParse(yAttr.Value, out double y))
                    {
                        if (y < minY)
                            minY = y;
                    }
                }
            }

            // 如果没有负数坐标，无需平移
            if (minX >= 0 && minY >= 0)
                return this;

            // 计算平移量（确保所有坐标变为正数）
            double translateX = Math.Abs(minX);
            double translateY = Math.Abs(minY);

            // 应用平移
            foreach (var element in elements)
            {
                // 更新所有 x 和 y 属性
                foreach (var attr in element.Attributes())
                {
                    if (attr.Name.LocalName.StartsWith("x"))
                    {
                        if (double.TryParse(attr.Value, out double x))
                        {
                            attr.Value = (x + translateX).ToString();
                        }
                    }
                    else if (attr.Name.LocalName.StartsWith("y"))
                    {
                        if (double.TryParse(attr.Value, out double y))
                        {
                            attr.Value = (y + translateY).ToString();
                        }
                    }
                }
                //更新 transform 属性
                var transformAttr = element.Attribute(svgNs + "transform");
                if (transformAttr != null)
                {
                    string transformValue = transformAttr.Value;
                    if (transformValue.Contains("translate"))
                    {
                        string[] parts = transformValue.Split(',');
                        if (parts.Length >= 2)
                        {
                            if (double.TryParse(parts[0].Split('(').Last(), out double currentTranslateX))
                                currentTranslateX += translateX;
                            if (double.TryParse(parts[1], out double currentTranslateY))
                                currentTranslateY += translateY;

                            transformValue = $"translate({currentTranslateX}, {currentTranslateY})";
                        }
                    }
                    else
                    {
                        transformValue = $"translate({translateX}, {translateY}) {transformValue}";
                    }
                    transformAttr.Value = transformValue;
                }
                else
                {
                    element.Add(new XAttribute(svgNs + "transform", $"translate({translateX}, {translateY})"));
                }
            }
            // 更新 viewBox 属性
            var viewBoxAttr = svgElement.Attribute("viewBox");
            if (viewBoxAttr != null)
            {
                string[] viewBoxParts = viewBoxAttr.Value.Split(' ');
                if (viewBoxParts.Length >= 4)
                {
                    if (double.TryParse(viewBoxParts[0], out double viewBoxX))
                        viewBoxX += translateX;
                    if (double.TryParse(viewBoxParts[1], out double viewBoxY))
                        viewBoxY += translateY;

                    viewBoxAttr.Value = $"{viewBoxX} {viewBoxY} {viewBoxParts[2]} {viewBoxParts[3]}";
                }
            }

            return this;
        }
        public string getSvgString()
        {
            if (markers.Count > 0)
            {
                var defs = new XElement(svgNs + "defs");
                foreach (var marker in markers.Values)
                {
                    defs.Add(marker);
                }
                svgElement.Add(defs);
            }
            svgElement.Add(elements);
            this.MoveToView();
            return svgElement.ToString(SaveOptions.DisableFormatting);
        }
        public void Save(string filePath)
        {
            if (markers.Count > 0)
            {
                var defs = new XElement(svgNs + "defs");
                foreach (var marker in markers.Values)
                {
                    defs.Add(marker);
                }
                svgElement.Add(defs);
            }

            svgElement.Add(elements);
            this.MoveToView();//移动到视口里
            svgElement.Save(filePath);
        }
    }
}