/*
    CSharpEditor - A C# source code editor with syntax highlighting, intelligent
    code completion and real-time compilation error checking.
    Copyright (C) 2021  Giorgio Bianchini
 
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, version 3.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using VectSharp;
using VectSharp.Canvas;

namespace CSharpEditor
{
    internal class FormattedText
    {
        public List<Paragraph> Paragraphs { get; } = new List<Paragraph>();

        public FormattedText()
        {

        }

        public Avalonia.Controls.Canvas Render(double maxWidth, bool renderAsControls, Canvas firstRowIconCanvas = null, bool indentSuccessiveLines = false)
        {
            Page pag = new Page(1, 1);

            Graphics gpr = pag.Graphics;

            maxWidth--;
            gpr.Translate(1, 0);

            double width = 0;

            double x = 0;
            double y = Paragraphs[0].Lines[0].GetAverageFontAscent();

            bool isFirstLine = true;

            for (int i = 0; i < Paragraphs.Count; i++)
            {
                y += Paragraphs[i].SpaceBefore;
                x = 0;

                for (int j = 0; j < Paragraphs[i].Lines.Count; j++)
                {
                    x = 0;
                    double runStartX = 0;

                    if (isFirstLine)
                    {
                        if (firstRowIconCanvas != null)
                        {
                            x += firstRowIconCanvas.Width + 5;
                            runStartX += firstRowIconCanvas.Width + 5;
                        }
                        isFirstLine = false;
                    }
                    else if (firstRowIconCanvas != null && indentSuccessiveLines)
                    {
                        x += firstRowIconCanvas.Width + 5;
                        runStartX += firstRowIconCanvas.Width + 5;
                    }

                    double spaceWidth = 0;

                    string currentRun = null;
                    Font currentFont = null;
                    Colour currentColour = Colour.FromRgba(0, 0, 0, 0);

                    for (int k = 0; k < Paragraphs[i].Lines[j].Words.Count; k++)
                    {
                        double wordWidth = 0;

                        for (int l = 0; l < Paragraphs[i].Lines[j].Words[k].SubWords.Count; l++)
                        {
                            if (!string.IsNullOrEmpty(Paragraphs[i].Lines[j].Words[k].SubWords[l].Text))
                            {
                                Font.DetailedFontMetrics subWordMetrics = Paragraphs[i].Lines[j].Words[k].SubWords[l].Font.MeasureTextAdvanced(Paragraphs[i].Lines[j].Words[k].SubWords[l].Text);
                                wordWidth += subWordMetrics.Width + subWordMetrics.LeftSideBearing + subWordMetrics.RightSideBearing;
                            }
                        }

                        if (x + wordWidth + spaceWidth > maxWidth)
                        {
                            if (!string.IsNullOrEmpty(currentRun))
                            {
                                gpr.FillText(runStartX, y, currentRun, currentFont, currentColour, TextBaselines.Baseline);
                            }

                            y += Paragraphs[i].Lines[j].Spacing;
                            x = 0;
                            runStartX = 0;
                            if (firstRowIconCanvas != null && indentSuccessiveLines)
                            {
                                x += firstRowIconCanvas.Width + 5;
                                runStartX += firstRowIconCanvas.Width + 5;
                            }
                            currentRun = null;
                            currentFont = null;
                            currentColour = Colour.FromRgba(0, 0, 0, 0);
                        }

                        x += wordWidth + spaceWidth;

                        width = Math.Max(x, width);

                        for (int l = 0; l < Paragraphs[i].Lines[j].Words[k].SubWords.Count; l++)
                        {
                            if (Paragraphs[i].Lines[j].Words[k].SubWords[l].Font == currentFont && Paragraphs[i].Lines[j].Words[k].SubWords[l].Colour == currentColour)
                            {
                                currentRun += (l == 0 ? " " : "") + Paragraphs[i].Lines[j].Words[k].SubWords[l].Text;
                                spaceWidth = currentFont.FontFamily.TrueTypeFile.Get1000EmGlyphWidth(' ') / 1000 * currentFont.FontSize;
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(currentRun))
                                {
                                    gpr.FillText(runStartX, y, currentRun, currentFont, currentColour, TextBaselines.Baseline);

                                    Font.DetailedFontMetrics runMetrics = currentFont.MeasureTextAdvanced(currentRun);

                                    runStartX = runStartX + runMetrics.Width + runMetrics.LeftSideBearing + runMetrics.RightSideBearing + (l == 0 ? spaceWidth : 0);
                                }

                                currentRun = Paragraphs[i].Lines[j].Words[k].SubWords[l].Text;
                                currentFont = Paragraphs[i].Lines[j].Words[k].SubWords[l].Font;
                                currentColour = Paragraphs[i].Lines[j].Words[k].SubWords[l].Colour;
                                spaceWidth = currentFont.FontFamily.TrueTypeFile.Get1000EmGlyphWidth(' ') / 1000 * currentFont.FontSize;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(currentRun))
                    {
                        gpr.FillText(runStartX, y, currentRun, currentFont, currentColour, TextBaselines.Baseline);
                    }

                    y += Paragraphs[i].Lines[j].Spacing;
                }

                y += Paragraphs[i].SpaceAfter;
            }

            pag.Height = y - Paragraphs[^1].Lines[^1].Spacing + Paragraphs[^1].Lines[^1].GetAverageFontSize() * 0.4;

            pag.Width = width + 1;

            Canvas can = pag.PaintToCanvas(renderAsControls, AvaloniaContextInterpreter.TextOptions.NeverConvert);
            can.ClipToBounds = false;

            if (firstRowIconCanvas != null)
            {
                firstRowIconCanvas.RenderTransform = new TranslateTransform(1, (Paragraphs[0].Lines[0].GetAverageFontAscent() - Paragraphs[0].Lines[0].GetAverageFontDescent() - firstRowIconCanvas.Height) * 0.5);
                can.Children.Add(firstRowIconCanvas);
            }


            return can;
        }

        public static IEnumerable<TaggedText> TokenizeText(string text)
        {
            string[] splitText = text.Split(' ');

            for (int i = 0; i < splitText.Length; i++)
            {
                yield return new TaggedText(TextTags.Text, splitText[i]);

                if (i < splitText.Length - 1)
                {
                    yield return new TaggedText(TextTags.Space, " ");
                }
            }
        }

        public static FormattedText FormatDescription(IEnumerable<TaggedText> taggedText, string documentationXml, VectSharp.Font labelFont, VectSharp.Font codeFont)
        {
            FormattedText fmt = new FormattedText();

            Paragraph p = new Paragraph();
            fmt.Paragraphs.Add(p);
            Line currentLine = new Line();
            p.Lines.Add(currentLine);

            Word currentWord = new Word();

            foreach (TaggedText text in taggedText)
            {
                switch (text.Tag)
                {
                    case TextTags.Keyword:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colour.FromRgb(0, 0, 255), text.Text));
                        break;

                    case TextTags.StringLiteral:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colour.FromRgb(163, 21, 21), text.Text));
                        break;

                    case TextTags.Punctuation:
                    case TextTags.Namespace:
                    case TextTags.Parameter:
                    case TextTags.Text:
                    case TextTags.EnumMember:
                    case TextTags.NumericLiteral:
                    case TextTags.Constant:
                    case TextTags.Property:
                    case TextTags.Method:
                    case TextTags.Event:
                    case TextTags.ErrorType:
                    case TextTags.Local:
                    case TextTags.Field:
                    case TextTags.Label:
                    case TextTags.RangeVariable:
                    case TextTags.ExtensionMethod:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colours.Black, text.Text));
                        break;

                    case TextTags.Space:
                        if (currentWord.SubWords.Count > 0)
                        {
                            currentLine.Words.Add(currentWord);
                            currentWord = new Word();
                        }
                        break;

                    case TextTags.LineBreak:
                        if (currentLine.Words.Count > 0)
                        {
                            if (currentWord.SubWords.Count > 0)
                            {
                                currentLine.Words.Add(currentWord);
                            }

                            if (string.IsNullOrWhiteSpace(documentationXml))
                            {
                                currentLine = new Line();
                                p.Lines.Add(currentLine);
                                currentWord = new Word();
                            }
                            else
                            {
                                currentLine = new Line();
                                currentWord = new Word();
                            }
                        }
                        break;

                    case TextTags.Class:
                    case TextTags.Delegate:
                    case TextTags.TypeParameter:
                    case TextTags.Struct:
                    case TextTags.Enum:
                    case TextTags.Interface:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colour.FromRgb(43, 145, 175), text.Text));
                        break;


                    case TextTags.Alias:
                    case TextTags.AnonymousTypeIndicator:
                    case TextTags.Assembly:
                    case TextTags.Module:
                    case TextTags.Operator:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colours.Black, text.Text));
                        break;

                    default:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colours.Black, text.Text));
                        break;
                }
            }

            if (currentWord.SubWords.Count > 0)
            {
                currentLine.Words.Add(currentWord);
            }

            FormatDocumentation(documentationXml, fmt, labelFont, codeFont);

            return fmt;
        }

        private static void FormatDocumentation(string documentationXml, FormattedText text, VectSharp.Font documentationFont, VectSharp.Font codeFont)
        {
            if (!string.IsNullOrEmpty(documentationXml))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<xml>" + documentationXml + "</xml>");

                List<Paragraph> summary = new List<Paragraph>();


                foreach (XmlNode elem in doc.DocumentElement.ChildNodes)
                {
                    if (elem.Name.Equals("summary", StringComparison.OrdinalIgnoreCase))
                    {
                        summary.AddRange(FormatDocumentationElement(elem, documentationFont, codeFont));
                    }
                    else if (elem.Name.Equals("member", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlNode elem2 in elem.ChildNodes)
                        {
                            if (elem2.Name.Equals("summary", StringComparison.OrdinalIgnoreCase))
                            {
                                summary.AddRange(FormatDocumentationElement(elem2, documentationFont, codeFont));
                            }
                        }
                    }
                }


                if (summary.Count > 0)
                {
                    summary[0].SpaceBefore += documentationFont.FontSize * 0.4;
                    text.Paragraphs.AddRange(summary);
                }
            }
        }

        public static FormattedText FormatParameterList(FormattedText text, string documentationXml, VectSharp.Font parameterNameFont, VectSharp.Font parameterDescriptionFont, VectSharp.Font codeFont)
        {
            if (!string.IsNullOrEmpty(documentationXml))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<xml>" + documentationXml + "</xml>");

                List<Paragraph> parameters = new List<Paragraph>();

                foreach (XmlNode elem in doc.DocumentElement.ChildNodes)
                {
                    if (elem.Name.Equals("param", StringComparison.OrdinalIgnoreCase))
                    {
                        string paramName = ((XmlElement)elem).GetAttribute("name");

                        Word nameWord = new Word();
                        nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                        List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem, parameterDescriptionFont, codeFont));

                        if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                        {
                            paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                            parameters.AddRange(paramParagraphs);
                        }
                    }
                    else if (elem.Name.Equals("member", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlNode elem2 in elem.ChildNodes)
                        {
                            if (elem2.Name.Equals("param", StringComparison.OrdinalIgnoreCase))
                            {
                                string paramName = ((XmlElement)elem2).GetAttribute("name");

                                Word nameWord = new Word();
                                nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                                List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem2, parameterDescriptionFont, codeFont));

                                if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                                {
                                    paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                                    parameters.AddRange(paramParagraphs);
                                }
                            }
                        }
                    }
                }

                if (parameters.Count > 0)
                {
                    text.Paragraphs.AddRange(parameters);
                }
            }

            return text;
        }

        public static FormattedText FormatTypeParameterList(FormattedText text, string documentationXml, VectSharp.Font parameterNameFont, VectSharp.Font parameterDescriptionFont, VectSharp.Font codeFont)
        {
            if (!string.IsNullOrEmpty(documentationXml))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<xml>" + documentationXml + "</xml>");

                List<Paragraph> parameters = new List<Paragraph>();

                foreach (XmlNode elem in doc.DocumentElement.ChildNodes)
                {
                    if (elem.Name.Equals("typeparam", StringComparison.OrdinalIgnoreCase))
                    {
                        string paramName = ((XmlElement)elem).GetAttribute("name");

                        Word nameWord = new Word();
                        nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                        List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem, parameterDescriptionFont, codeFont));

                        if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                        {
                            paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                            parameters.AddRange(paramParagraphs);
                        }
                    }
                    else if (elem.Name.Equals("member", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlNode elem2 in elem.ChildNodes)
                        {
                            if (elem2.Name.Equals("typeparam", StringComparison.OrdinalIgnoreCase))
                            {
                                string paramName = ((XmlElement)elem2).GetAttribute("name");

                                Word nameWord = new Word();
                                nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                                List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem2, parameterDescriptionFont, codeFont));

                                if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                                {
                                    paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                                    parameters.AddRange(paramParagraphs);
                                }
                            }
                        }
                    }
                }

                if (parameters.Count > 0)
                {
                    text.Paragraphs.AddRange(parameters);
                }
            }

            return text;
        }

        private static IEnumerable<Paragraph> FormatDocumentationElement(XmlNode elem, VectSharp.Font documentationFont, VectSharp.Font codeFont)
        {
            Paragraph currentParagraph = null;

            foreach (XmlNode child in elem.ChildNodes)
            {
                if (child is XmlText)
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    if (!string.IsNullOrWhiteSpace(child.InnerText))
                    {
                        List<Word> newWords = Word.GetWords(child.InnerText, documentationFont, VectSharp.Colours.Black).ToList();

                        if (newWords.Count > 0)
                        {
                            if (currentParagraph.Lines[^1].Words.Count > 0 && newWords[0].SubWords[0].Text.Length == 1 && char.IsPunctuation(newWords[0].SubWords[0].Text[0]))
                            {
                                currentParagraph.Lines[^1].Words[^1].SubWords.AddRange(newWords[0].SubWords);
                                currentParagraph.Lines[^1].Words.AddRange(newWords.Skip(1));
                            }
                            else
                            {
                                currentParagraph.Lines[^1].Words.AddRange(newWords);
                            }
                        }
                    }
                }
                else if (child.Name.Equals("see", StringComparison.OrdinalIgnoreCase) || child.Name.Equals("seealso", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    string cref = ((XmlElement)child).GetAttribute("cref");
                    string langword = ((XmlElement)child).GetAttribute("langword");
                    string href = ((XmlElement)child).GetAttribute("href");

                    if (!string.IsNullOrEmpty(cref))
                    {
                        if (cref.StartsWith("T:"))
                        {
                            string prefix = cref.Substring(2);
                            string typeName = prefix.Substring(prefix.LastIndexOf(".") + 1);
                            prefix = prefix.Substring(0, prefix.LastIndexOf(".") + 1);

                            string suffix = "";

                            if (typeName.Contains("`"))
                            {
                                suffix = typeName.Substring(typeName.IndexOf("`"));
                                typeName = typeName.Substring(0, typeName.IndexOf("`"));
                            }

                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, prefix));
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(43, 145, 175), typeName));
                            if (!string.IsNullOrEmpty(suffix))
                            {
                                w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, suffix));
                            }
                            currentParagraph.Lines[^1].Words.Add(w);
                        }
                        else if (cref.StartsWith("F:") || cref.StartsWith("E:"))
                        {
                            string prefix = cref.Substring(2);
                            string suffix = prefix.Substring(prefix.LastIndexOf("."));
                            prefix = prefix.Substring(0, prefix.LastIndexOf("."));

                            string typeName = prefix.Substring(prefix.LastIndexOf(".") + 1);
                            prefix = prefix.Substring(0, prefix.LastIndexOf(".") + 1);

                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, prefix));
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(43, 145, 175), typeName));
                            if (!string.IsNullOrEmpty(suffix))
                            {
                                w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, suffix));
                            }
                            currentParagraph.Lines[^1].Words.Add(w);
                        }
                        else if (cref.StartsWith("N:"))
                        {
                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, cref.Substring(2)));
                            currentParagraph.Lines[^1].Words.Add(w);
                        }
                        else if (cref.StartsWith("P:") || cref.StartsWith("M:"))
                        {
                            string sufsuffix = "";

                            string prefix = cref.Substring(2);
                            if (prefix.Contains("("))
                            {
                                sufsuffix = prefix.Substring(prefix.IndexOf("("));
                                prefix = prefix.Substring(0, prefix.IndexOf("("));
                            }

                            string suffix = prefix.Substring(prefix.LastIndexOf(".")) + sufsuffix;
                            prefix = prefix.Substring(0, prefix.LastIndexOf("."));

                            string typeName = prefix.Substring(prefix.LastIndexOf(".") + 1);
                            prefix = prefix.Substring(0, prefix.LastIndexOf(".") + 1);

                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, prefix));
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(43, 145, 175), typeName));
                            if (!string.IsNullOrEmpty(suffix))
                            {
                                w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, suffix));
                            }
                            currentParagraph.Lines[^1].Words.Add(w);
                        }
                        else
                        {
                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, cref));
                            currentParagraph.Lines[^1].Words.Add(w);
                        }
                    }
                    else if (!string.IsNullOrEmpty(langword))
                    {
                        Word w = new Word();
                        w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(0, 0, 255), langword));
                        currentParagraph.Lines[^1].Words.Add(w);
                    }
                    else if (!string.IsNullOrEmpty(href))
                    {
                        Word w = new Word();
                        w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(0, 0, 255), href));
                        currentParagraph.Lines[^1].Words.Add(w);
                    }
                }
                else if (child.Name.Equals("c", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    List<Word> newWords = Word.GetWords(child.InnerText, codeFont, VectSharp.Colours.Black).ToList();

                    if (newWords.Count > 0)
                    {
                        currentParagraph.Lines[^1].Words.AddRange(newWords);
                    }
                }
                else if (child.Name.Equals("paramref", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    string name = ((XmlElement)child).GetAttribute("name");

                    if (!string.IsNullOrEmpty(name))
                    {
                        Word w = new Word();
                        w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, name));
                        currentParagraph.Lines[^1].Words.Add(w);
                    }
                }
            }

            if (currentParagraph != null)
            {
                yield return currentParagraph;
            }
        }
    }

    internal class SubWord
    {
        public Font Font { get; }
        public Colour Colour { get; }
        public string Text { get; }

        public SubWord(Font font, Colour colour, string text)
        {
            Font = font;
            Colour = colour;
            Text = text;
        }
    }

    internal class Word
    {
        public List<SubWord> SubWords { get; } = new List<SubWord>();

        public static IEnumerable<Word> GetWords(string text, Font font, Colour colour)
        {
            text = text.Replace("\t", " ").Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
            foreach (string sr in text.Split(' '))
            {
                if (!string.IsNullOrEmpty(sr))
                {
                    Word w = new Word();
                    w.SubWords.Add(new SubWord(font, colour, sr));
                    yield return w;
                }
            }
        }
    }

    internal class Line
    {
        public List<Word> Words { get; } = new List<Word>();

        private double _spacing = double.NaN;

        public double Spacing
        {
            get
            {
                if (!double.IsNaN(_spacing))
                {
                    return _spacing;
                }
                else
                {
                    return GetAverageFontSize() * 1.4;
                }
            }
            set
            {
                _spacing = value;
            }
        }

        public double GetAverageFontSize()
        {
            double tbr = 0;

            int count = 0;

            for (int i = 0; i < Words.Count; i++)
            {
                for (int j = 0; j < Words[i].SubWords.Count; j++)
                {
                    tbr += Words[i].SubWords[j].Font.FontSize;
                    count++;
                }
            }

            return tbr / count;
        }

        public double GetAverageFontAscent()
        {
            double tbr = 0;

            int count = 0;

            for (int i = 0; i < Words.Count; i++)
            {
                for (int j = 0; j < Words[i].SubWords.Count; j++)
                {
                    tbr += Words[i].SubWords[j].Font.Ascent;
                    count++;
                }
            }

            return tbr / count;
        }

        public double GetAverageFontDescent()
        {
            double tbr = 0;

            int count = 0;

            for (int i = 0; i < Words.Count; i++)
            {
                for (int j = 0; j < Words[i].SubWords.Count; j++)
                {
                    tbr += Words[i].SubWords[j].Font.Descent;
                    count++;
                }
            }

            return tbr / count;
        }
    }

    internal class Paragraph
    {
        public List<Line> Lines { get; } = new List<Line>();
        public double SpaceBefore { get; set; } = 0;
        public double SpaceAfter { get; set; } = 0;
    }
}
