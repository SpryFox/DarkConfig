using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DarkConfig {
    public static class UnityFromDocs {
        public static void RegisterAll() {
            Config.Register<Vector2>(FromVector2);
            Config.Register<Vector3>(FromVector3);
            Config.Register<Color>(FromColor);
        }

        public static object FromVector2(object obj, DocNode value) {
            DocNodeType parsedType = value.Type;
            if (parsedType == DocNodeType.Scalar) {   // Vector2, 3 => new Vector2(3,3);
                var single = value.AsFloat();
                return new Vector2(single, single);
            } else {                      // Vector2, [1,2] => new Vector2(1,2);
                float v1 = value[0].AsFloat();
                float v2 = v1;
                if(value.Count > 1) {
                    v2 = value[1].AsFloat();
                }
                return new Vector2(v1, v2);
            }
        }

        public static object FromVector3(object obj, DocNode value) {
            DocNodeType parsedType = value.Type;
            if (parsedType == DocNodeType.Scalar) {   // Vector3, 3 => new Vector3(3,3, 3);
                float single = value.AsFloat();
                return new Vector3(single, single, single);
            } else {                      // Vector3, [1,2,3] => new Vector2(1,2,3);
                float v1 = value[0].AsFloat();
                float v2 = v1;
                float v3 = v1;
                if(value.Count > 1) {
                    v2 = value[1].AsFloat();
                    v3 = 0;
                }    
                if (value.Count > 2) {
                    v3 = value[2].AsFloat();
                }
                return new Vector3(v1, v2, v3);
            }
        }


        public static Color32 ParseColor32(string str) {
            // try hex first
            if (string.IsNullOrEmpty(str)) return new Color32(0, 0, 0, 0);
    
            try{
                str = str.Replace("0x", "");
                str = str.Replace("#", "");
                byte a = 255;
                byte r = byte.Parse(str.Substring(0,2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(str.Substring(2,2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(str.Substring(4,2), System.Globalization.NumberStyles.HexNumber);
                if(str.Length >= 8){
                    a = byte.Parse(str.Substring(6,2), System.Globalization.NumberStyles.HexNumber);
                }
                return new Color32(r,g,b,a);
            } catch {
            }
            
            // hex didn't work, try it as comma-separated bytes (as floats but 0-255)
            try{
                var parts = str.Split(new char[] {','});
                var nums = new float[parts.Length];
                
                for(int i = 0; i < parts.Length; i++) {
                    nums[i] = Convert.ToSingle(parts[i], System.Globalization.CultureInfo.InvariantCulture);
                }

                if(parts.Length == 3) {
                    return new Color32((byte)nums[0], (byte)nums[1], (byte)nums[2], 255);
                }
                return new Color32((byte)nums[0], (byte)nums[1], (byte)nums[2], (byte)nums[2]);
            } catch {
            }

            return new Color32();
        }

        public static object FromColor(object obj, DocNode value) {
            if(value.Type == DocNodeType.Scalar) {
                return (Color)ParseColor32(value.StringValue);
            }

            var c = value.Values.Select(x => x.AsFloat()).ToList();

            // see if any of the values are over 1; if so, treat each as if they're in the range 0-255
            bool isBytes = false;
            for(int i = 0; i < c.Count; i++) {
                if(c[i] > 1) isBytes = true;
            }
            for(int i = 0; i < c.Count; i++) {
                if(isBytes) c[i] = c[i]/255;
            }

            // Color, [1,1,1] => new Color(1,1,1,1)
            if (c.Count == 3) {
                return new Color(c[0], c[1], c[2]);
            }
            // Color, [1,1,1,1] => new Color(1,1,1,1)
            if (c.Count == 4) {
                return new Color(c[0], c[1], c[2], c[3]);
            }
            return Color.magenta;
        }
    }
}