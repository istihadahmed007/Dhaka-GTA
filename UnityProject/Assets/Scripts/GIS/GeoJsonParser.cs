using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DhakaGta.GIS
{
    /// <summary>
    /// Zero-dependency RFC 8259 JSON parser tailored for GeoJSON FeatureCollections.
    /// Supports nested coordinate arrays (Point / LineString / Polygon / MultiPolygon) that Unity JsonUtility cannot parse.
    /// </summary>
    public static class GeoJsonParser
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            int index = 0;
            return ParseValue(json, ref index);
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i]))
            {
                i++;
            }
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            if (i >= s.Length)
            {
                return null;
            }

            char c = s[i];
            return c switch
            {
                '{' => ParseObject(s, ref i),
                '[' => ParseArray(s, ref i),
                '"' => ParseString(s, ref i),
                't' or 'f' => ParseBool(s, ref i),
                'n' => ParseNull(s, ref i),
                _ => ParseNumber(s, ref i),
            };
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // consume '{'
            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == '}')
                {
                    i++;
                    return dict;
                }

                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ':')
                {
                    i++;
                }

                object value = ParseValue(s, ref i);
                if (key != null)
                {
                    dict[key] = value;
                }

                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                }
            }
            return dict;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // consume '['
            while (i < s.Length)
            {
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ']')
                {
                    i++;
                    return list;
                }

                list.Add(ParseValue(s, ref i));
                SkipWhitespace(s, ref i);
                if (i < s.Length && s[i] == ',')
                {
                    i++;
                }
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"')
            {
                return string.Empty;
            }

            i++; // consume '"'
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c == '\\' && i < s.Length)
                {
                    char esc = s[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u' when i + 4 <= s.Length:
                            string hex = s.Substring(i, 4);
                            if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ushort code))
                            {
                                sb.Append((char)code);
                            }
                            i += 4;
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private static object ParseBool(string s, ref int i)
        {
            if (s.IndexOf("true", i, StringComparison.Ordinal) == i)
            {
                i += 4;
                return true;
            }
            if (s.IndexOf("false", i, StringComparison.Ordinal) == i)
            {
                i += 5;
                return false;
            }
            return false;
        }

        private static object ParseNull(string s, ref int i)
        {
            if (s.IndexOf("null", i, StringComparison.Ordinal) == i)
            {
                i += 4;
            }
            return null;
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                {
                    i++;
                }
                else
                {
                    break;
                }
            }

            string token = s.Substring(start, Math.Max(0, i - start));
            return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0.0;
        }
    }
}
