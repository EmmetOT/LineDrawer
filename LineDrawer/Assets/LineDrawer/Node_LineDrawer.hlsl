#ifndef LINE_DRAWER_NODE_INCLUDED
#define LINE_DRAWER_NODE_INCLUDED

struct PointData
{
    float2 Position;
    float Width;
    float4 Colour;
};

StructuredBuffer<PointData> _PointDataBuffer;

float LineSDF(float2 p, float2 a, float2 b, out float t)
{
    float2 pa = p - a, ba = b - a;
    t = saturate(dot(pa, ba) / dot(ba, ba));
    return length(pa - ba * t);
}

void DrawLine_float(in float2 Position, out float SignedDistance, out float4 Colour)
{
    SignedDistance = 10000000.0;
    Colour = 0;
    
    float partialDerivative = length(fwidth(Position).xy);
    
    for (int i = 0; i < _PointCount - 1; i++)
    {
        PointData a = _PointDataBuffer[i];
        PointData b = _PointDataBuffer[i + 1];
        
        float t;
        float dist = LineSDF(Position, a.Position.xy, b.Position.xy, t);
        dist -= partialDerivative * lerp(a.Width, b.Width, t);
        
        if (dist < SignedDistance)
        {
            SignedDistance = dist;
            Colour = lerp(a.Colour, b.Colour, t);
        }
    }
}

#endif // LINE_DRAWER_NODE_INCLUDED