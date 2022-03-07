using UnityEngine;
using System;
using System.Collections.Generic;

namespace DarkConfig {
    /// Config reifiers for built-in Unity types.
    public static class UnityTypeReifiers {
        public static void RegisterAll() {
            Configs.RegisterFromDoc<Vector2>(FromVector2);
            Configs.RegisterFromDoc<Vector3>(FromVector3);
            Configs.RegisterFromDoc<Color>(FromColor);
        }

        static object FromVector2(object obj, DocNode value) {
            // TODO (Graham): Make a non-boxing version of this?
            // TODO (Graham): The scalar and n-dimensional support here is non-obvious. Those cases seem like they'd be nearly always mistakes. Better to throw an exception here.
            
            var parsedType = value.Type;
            
            // Parse a scalar float and use that for both components of the vector.
            // 3 => new Vector2(3,3)
            if (parsedType == DocNodeType.Scalar) {
                var single = value.As<float>();
                return new Vector2(single, single);
            } 

            // Parse a list of floats and use those as the vector components.
            // Supports the following conversions:
            // [1] => Vector2(1,1)
            // [1,2] => new Vector2(1,2);
            // [1,2,3,4,5,6] => new Vector2(1,2);
            float x = value[0].As<float>();
            float y = x;
            if (value.Count > 1) {
                y = value[1].As<float>();
            }

            return new Vector2(x, y);
        }

        static object FromVector3(object obj, DocNode value) {
            // TODO (Graham): Make a non-boxing version of this?
            // TODO (Graham): The scalar and n-dimensional support here is non-obvious. Those cases seem like they'd be nearly always mistakes. Better to throw an exception here.

            var parsedType = value.Type;
            if (parsedType == DocNodeType.Scalar) { // Vector3, 3 => new Vector3(3,3, 3);
                float single = value.As<float>();
                return new Vector3(single, single, single);
            }
            
            // Vector3, [1,2,3] => new Vector2(1,2,3);
            float x = value[0].As<float>();
            float y = x;
            float z = x;
            if (value.Count > 1) {
                y = value[1].As<float>();
                z = 0;
            }

            if (value.Count > 2) {
                z = value[2].As<float>();
            }

            return new Vector3(x, y, z);
        }

        static Color32 ParseColor32(string str) {
            // Default to black if there's no string to parse.
            if (string.IsNullOrEmpty(str)) {
                return new Color32(0, 0, 0, 0);
            }

            // try hex first
            try {
                var hexString = str.Replace("0x", "");
                hexString = hexString.Replace("#", "");
                byte a = 255;
                byte r = byte.Parse(hexString.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hexString.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hexString.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                if (hexString.Length >= 8) {
                    a = byte.Parse(hexString.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                }

                return new Color32(r, g, b, a);
            } catch {
                // ignored
            }

            // hex didn't work, try it as comma-separated bytes (as floats but 0-255)
            try {
                var parts = str.Split(',');
                var nums = new float[parts.Length];

                for (int i = 0; i < parts.Length; i++) {
                    nums[i] = Convert.ToSingle(parts[i], System.Globalization.CultureInfo.InvariantCulture);
                }

                if (parts.Length == 3) {
                    return new Color32((byte) nums[0], (byte) nums[1], (byte) nums[2], 255);
                }

                return new Color32((byte) nums[0], (byte) nums[1], (byte) nums[2], (byte) nums[2]);
            } catch {
                // ignored
            }

            // TODO (Graham): Throw an exception here?  We couldn't parse the string into a color so better to complain loudly than fail silently.
            
            return new Color32();
        }

        static object FromColor(object obj, DocNode value) {
            if (value.Type == DocNodeType.Scalar) {
                return (Color) ParseColor32(value.StringValue);
            }

            var colorValues = new List<float>();
            foreach (var docValue in value.Values) {
                colorValues.Add(docValue.As<float>());
            }

            // see if any of the values are over 1; if so, treat each as if they're in the range 0-255 instead of 0-1
            bool isBytes = false;
            foreach (float colorVal in colorValues) {
                isBytes |= colorVal > 1; 
            }

            // If color values are in the 0-255 range, normalize them to 0-1
            if (isBytes) { 
                for (int i = 0; i < colorValues.Count; i++) {
                    colorValues[i] = colorValues[i] / 255;
                }
            }

            // Color, [1,1,1] => new Color(1,1,1,1)
            if (colorValues.Count == 3) {
                return new Color(colorValues[0], colorValues[1], colorValues[2]);
            }

            // Color, [1,1,1,1] => new Color(1,1,1,1)
            if (colorValues.Count == 4) {
                return new Color(colorValues[0], colorValues[1], colorValues[2], colorValues[3]);
            }

            // TODO (Graham): Throw an exception here?  We couldn't parse the value into a color so better to complain loudly than fail silently.
            
            return Color.magenta;
        }
    }
}