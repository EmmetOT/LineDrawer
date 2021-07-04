using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AALineDrawer
{
    [ExecuteInEditMode]
    public class LineDrawTest : MonoBehaviour
    {
        [SerializeField]
        private bool m_isAnimating = false;

        [SerializeField]
        private LineDrawer m_lineDrawer = null;

        [SerializeField]
        [HideInInspector]
        private LineDrawer.PointData[] m_points = null;

        //private void OnValidate() => SendData();

        [Header("Animation Settings")]

        [SerializeField]
        private float m_rotateSpeed = 2f;

        [SerializeField]
        private float m_pulseSpeed = 2f;

        [SerializeField]
        [Min(5)]
        private int m_pointCount = 60;

        [SerializeField]
        [Min(0.1f)]
        private float m_radius = 6f;

        [SerializeField]
        [Min(0f)]
        private float m_minWidth = 1f;

        [SerializeField]
        [Min(0f)]
        private float m_maxWidth = 7f;

        [SerializeField]
        [Min(1)]
        private int m_widthPeriod = 6;
        
        private void Update()
        {
            if (m_points == null || m_points.Length == 0 || !m_lineDrawer || !m_isAnimating)
                return;
            
            UpdateRainbowCircle(Time.time * m_rotateSpeed, Time.time * m_pulseSpeed);
        }

        private void OnValidate()
        {
            if (m_points == null || m_points.Length == 0 || m_points.Length != m_pointCount)
                m_points = new LineDrawer.PointData[m_pointCount];
            
            UpdateRainbowCircle(Time.time * m_rotateSpeed, Time.time * m_pulseSpeed);
        }

        private void UpdateRainbowCircle(float rotateTime, float pulseTime)
        {
            float pulse = (Mathf.Cos(pulseTime * Mathf.PI * 2f) + 1f) * 0.5f;
            float min = Mathf.Lerp(m_minWidth, m_maxWidth, pulse);
            float max = Mathf.Lerp(m_minWidth, m_maxWidth, 1f - pulse);

            float MinMaxSine(float t) => Mathf.Sin(t * Mathf.PI * 2f * m_widthPeriod) * (0.5f * max - min) + (0.5f * max + min);

            for (int i = 0; i < m_points.Length; i++)
            {
                float t = i / (m_points.Length - 1f);

                m_points[i] = new LineDrawer.PointData()
                {
                    Position = m_radius * new Vector2(Mathf.Cos(t * Mathf.PI * 2f), Mathf.Sin(t * Mathf.PI * 2f)),
                    Width = MinMaxSine(t + rotateTime),
                    Colour = ColourStuff.EvaluateRainbow(t)
                };
            }

            if (m_lineDrawer)
                m_lineDrawer.SetPoints(m_points);
        }


        #region Test Colour Stuff

        private static class ColourStuff
        {
            private static Gradient m_rainbowGradient;

            static ColourStuff()
            {
                m_rainbowGradient = CreateGradient(
                    Color.red,
                    ToColor(0xFF8200),
                    Color.yellow,
                    Color.green,
                    ToColor(0x005DFF),
                    ToColor(0xAE00FF),
                    ToColor(0xFF00CB),
                    Color.red
                    );
            }

            public static Color EvaluateRainbow(float t) => m_rainbowGradient.Evaluate(t % 1f);

            /// <summary>
            /// Creates a gradient of the given colours, distributed evenly.
            /// </summary>
            public static Gradient CreateGradient(params Color[] colours)
            {
                Gradient gradient = new Gradient();

                GradientColorKey[] colourKeys = new GradientColorKey[colours.Length];

                for (int i = 0; i < colours.Length; i++)
                {
                    colourKeys[i].color = colours[i];
                    colourKeys[i].time = i / (colours.Length - 1f);
                }

                GradientAlphaKey[] alphaKey = new GradientAlphaKey[1];

                for (int i = 0; i < alphaKey.Length; i++)
                    alphaKey[i].alpha = 1f;

                gradient.SetKeys(colourKeys, alphaKey);

                return gradient;
            }

            /// <summary>
            /// Convert an RGB hex value to a Color.
            /// </summary>
            public static Color ToColor(int hexVal)
            {
                float R = ((byte)((hexVal >> 16) & 0xFF)) / 255f;
                float G = ((byte)((hexVal >> 8) & 0xFF)) / 255f;
                float B = ((byte)((hexVal) & 0xFF)) / 255f;
                return new Color(R, G, B, 1f);
            }

        }

        #endregion

        //        [System.Serializable]
        //        private struct PointTest
        //        {
        //#pragma warning disable 0649
        //            public Transform Transform;
        //            public float Width;
        //            public Color Colour;
        //#pragma warning restore 0649

        //            public static implicit operator LineDrawer.PointData(PointTest pointTest)
        //            {
        //                return new LineDrawer.PointData()
        //                {
        //                    Position = pointTest.Transform.position,
        //                    Width = Mathf.Max(0f, pointTest.Width),
        //                    Colour = pointTest.Colour
        //                };
        //            }
        //        }

    }
}