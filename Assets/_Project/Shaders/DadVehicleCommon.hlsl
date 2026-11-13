// What Dad/Vehicle and Dad/VehicleGlass share (E-05): the per-instance numbers and the vertex format the vehicle kit writes
// (Runtime/Traffic/VehicleKit.cs): position, normal, an sRGB colour with the material in alpha (material * 32 + 16), and the
// lamp number in uv.x. Include after Core.hlsl.
#ifndef DAD_VEHICLE_COMMON
#define DAD_VEHICLE_COMMON

UNITY_INSTANCING_BUFFER_START(DadVehicle)
    UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)   // rgb: paint (linear); a: how much road dust
    UNITY_DEFINE_INSTANCED_PROP(float4, _Lamps)  // headlamps, brakes, left and right indicators: 0 to 1
    UNITY_DEFINE_INSTANCED_PROP(float4, _Finish) // the paint's smoothness, metallic, flake, pearl
    UNITY_DEFINE_INSTANCED_PROP(float4, _Second) // rgb: second tone (linear); a: metres up the body it stops, 0 for none
UNITY_INSTANCING_BUFFER_END(DadVehicle)

#define KIT_PAINT 0
#define KIT_PLASTIC 1
#define KIT_GLASS 2
#define KIT_METAL 3
#define KIT_LENS 4
#define KIT_ROUGH 5
#define KIT_RUBBER 6
#define KIT_TRIM 7

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    half4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 map : TEXCOORD1; // the place in the family's picture, where it has one
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

uint KitMaterial(half alpha) { return (uint)(alpha * 255.0 + 0.5) >> 5; }

#endif
