Shader "Kaleidos/cumulus fractus 2D URP" {
    Properties {
        _Zoom ("Zoom", Float) = 1
        _Pan ("Pan", Vector) = (0,0,0,0)
        _Rotation ("Rotation", Float) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        _Speed ("Speed", Float) = 1
        _CellSize ("Pixel Size", Float) = 2.0
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Zoom;
            float4 _Pan;
            float _Rotation;
            float4 _Tint;
            float _Speed;
            float _CellSize;

            #define uTime (_Time.y * _Speed)
            #define uResolution float2(640.0, 360.0)
            #define cellSize _CellSize

float glslMod(float a, float b){ return a - b * floor(a / b); }

#define time uTime
static const float noiseScale = 3.0;
#define screenSize float2(640.0, 360.0)
#define sampleOffset (float2)(0.0)
#define renderSize float2(640.0, 360.0)

#define morphPhase (uTime*0.05)
#define spinPhase (uTime*0.0)
static const float orbitRadius = 0.6;
#define scrollSpeed float2(0.15, 0.15)
static const float2 loopD = float2(0.0, 0.0);
static const float jitter = 0.8;
static const float minkowskiP = 1.8;
static const float starPoints = 6.0;
static const float starDepth = 0.0;
static const float cellStretch = 1.0;
static const float cellAngle = 67.5;
static const float cellRadius = 0.6;
static const float smoothK = 0.3;
static const float edgeSharpen = 3.0;
static const float octaves = 5.0;
static const float gain = 0.55;
static const float pinchStrength = -0.6;
#define warpPhase (uTime*0.0)

static const float4 PALETTE[110] = {
  float4(0.43352,0.651637,0.93551,1.0),
  float4(0.433532,0.651644,0.935509,1.0),
  float4(0.433561,0.651661,0.935508,1.0),
  float4(0.433615,0.651694,0.935505,1.0),
  float4(0.433711,0.651751,0.9355,1.0),
  float4(0.433874,0.651849,0.935491,1.0),
  float4(0.434141,0.65201,0.935477,1.0),
  float4(0.434566,0.652264,0.935454,1.0),
  float4(0.435218,0.652656,0.93542,1.0),
  float4(0.43619,0.65324,0.935369,1.0),
  float4(0.437595,0.654084,0.935296,1.0),
  float4(0.439566,0.655269,0.935194,1.0),
  float4(0.442252,0.656883,0.935056,1.0),
  float4(0.445809,0.659023,0.934875,1.0),
  float4(0.450391,0.661779,0.934643,1.0),
  float4(0.456132,0.665235,0.934355,1.0),
  float4(0.463134,0.669451,0.934007,1.0),
  float4(0.471448,0.67446,0.933599,1.0),
  float4(0.506685,0.696162,0.938025,1.0),
  float4(0.541922,0.717865,0.942452,1.0),
  float4(0.577158,0.739568,0.946879,1.0),
  float4(0.612395,0.76127,0.951306,1.0),
  float4(0.647632,0.782973,0.955732,1.0),
  float4(0.682869,0.804676,0.960159,1.0),
  float4(0.718106,0.826378,0.964586,1.0),
  float4(0.753342,0.848081,0.969013,1.0),
  float4(0.788579,0.869784,0.973439,1.0),
  float4(0.823816,0.891487,0.977866,1.0),
  float4(0.859053,0.913189,0.982293,1.0),
  float4(0.89429,0.934892,0.98672,1.0),
  float4(0.929526,0.956595,0.991146,1.0),
  float4(0.964763,0.978297,0.995573,1.0),
  float4(1.0,1.0,1.0,1.0),
  float4(1.0,1.0,1.0,1.0),
  float4(1.0,1.0,1.0,1.0),
  float4(1.0,1.0,1.0,1.0),
  float4(1.0,1.0,1.0,1.0),
  float4(1.0,1.0,1.0,1.0),
  float4(0.986595,0.99181,0.996941,1.0),
  float4(0.973189,0.98362,0.993882,1.0),
  float4(0.959784,0.97543,0.990823,1.0),
  float4(0.946379,0.967239,0.987764,1.0),
  float4(0.932973,0.959049,0.984705,1.0),
  float4(0.919568,0.950859,0.981646,1.0),
  float4(0.906163,0.942669,0.978587,1.0),
  float4(0.892757,0.934479,0.975528,1.0),
  float4(0.879352,0.926289,0.972469,1.0),
  float4(0.865947,0.918099,0.96941,1.0),
  float4(0.852541,0.909908,0.966352,1.0),
  float4(0.839136,0.901718,0.963293,1.0),
  float4(0.825731,0.893528,0.960234,1.0),
  float4(0.812325,0.885338,0.957175,1.0),
  float4(0.79892,0.877148,0.954116,1.0),
  float4(0.785515,0.868958,0.951057,1.0),
  float4(0.77211,0.860768,0.947998,1.0),
  float4(0.758704,0.852577,0.944939,1.0),
  float4(0.745299,0.844387,0.94188,1.0),
  float4(0.731894,0.836197,0.938821,1.0),
  float4(0.718488,0.828007,0.935762,1.0),
  float4(0.705083,0.819817,0.932703,1.0),
  float4(0.710496,0.823212,0.932823,1.0),
  float4(0.715909,0.826607,0.932943,1.0),
  float4(0.721322,0.830002,0.933063,1.0),
  float4(0.726735,0.833397,0.933182,1.0),
  float4(0.732149,0.836793,0.933302,1.0),
  float4(0.737562,0.840188,0.933422,1.0),
  float4(0.742975,0.843583,0.933542,1.0),
  float4(0.748388,0.846978,0.933662,1.0),
  float4(0.753801,0.850373,0.933782,1.0),
  float4(0.759214,0.853768,0.933902,1.0),
  float4(0.764627,0.857164,0.934021,1.0),
  float4(0.77004,0.860559,0.934141,1.0),
  float4(0.775454,0.863954,0.934261,1.0),
  float4(0.780867,0.867349,0.934381,1.0),
  float4(0.78628,0.870744,0.934501,1.0),
  float4(0.791693,0.874139,0.934621,1.0),
  float4(0.797106,0.877535,0.934741,1.0),
  float4(0.802519,0.88093,0.93486,1.0),
  float4(0.807932,0.884325,0.93498,1.0),
  float4(0.813345,0.88772,0.9351,1.0),
  float4(0.818759,0.891115,0.93522,1.0),
  float4(0.824172,0.894511,0.93534,1.0),
  float4(0.829585,0.897906,0.93546,1.0),
  float4(0.834998,0.901301,0.935579,1.0),
  float4(0.840411,0.904696,0.935699,1.0),
  float4(0.845824,0.908091,0.935819,1.0),
  float4(0.851237,0.911486,0.935939,1.0),
  float4(0.85665,0.914882,0.936059,1.0),
  float4(0.862064,0.918277,0.936179,1.0),
  float4(0.867477,0.921672,0.936299,1.0),
  float4(0.87289,0.925067,0.936418,1.0),
  float4(0.878303,0.928462,0.936538,1.0),
  float4(0.883716,0.931857,0.936658,1.0),
  float4(0.889129,0.935253,0.936778,1.0),
  float4(0.894542,0.938648,0.936898,1.0),
  float4(0.899955,0.942043,0.937018,1.0),
  float4(0.905369,0.945438,0.937137,1.0),
  float4(0.910782,0.948833,0.937257,1.0),
  float4(0.916195,0.952228,0.937377,1.0),
  float4(0.921608,0.955624,0.937497,1.0),
  float4(0.927021,0.959019,0.937617,1.0),
  float4(0.932434,0.962414,0.937737,1.0),
  float4(0.937847,0.965809,0.937857,1.0),
  float4(0.94326,0.969204,0.937976,1.0),
  float4(0.948674,0.972599,0.938096,1.0),
  float4(0.954087,0.975995,0.938216,1.0),
  float4(0.9595,0.97939,0.938336,1.0),
  float4(0.964913,0.982785,0.938456,1.0),
  float4(0.970326,0.98618,0.938576,1.0),
  float4(0.975739,0.989575,0.938696,1.0)
};
float4 paletteLookup(float x){
  int i = clamp(int(clamp(x,0.0,1.0)*256.0),0,255);
  return PALETTE[clamp(int(float(i)/255.0*110.0),0,109)];
}

#define TAU 6.28318530718
#define MAX_OCTAVES 10

float2 frameC(float2 uv) {
    float2 r = renderSize;
    return (uv - 0.5) * screenSize / r.y;
}
float2 frameUV(float2 c) {
    float2 r = renderSize;
    return 0.5 + c * r.y / screenSize;
}

float2 latticeOffset(float2 cell) {
    float row = floor(cell.y);
    
    return float2(glslMod(row, 2.0) * 0.5, 0.0);
}

float2 hash2(float2 p) {
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

float2 wrapCell(float2 cell, float angN, float2 d) {
    if (angN > 0.5) cell.x = glslMod(cell.x, angN);
    
    return cell;
}


float metricDist(float2 d, float ang) {
    float s = sin(ang), c = cos(ang);
    d = mul(d, float2x2(c, -s, s, c));
    d.x *= cellStretch; d.y /= cellStretch;
    float p  = max(minkowskiP, 0.1);
    float md = pow(pow(abs(d.x), p) + pow(abs(d.y), p), 1.0 / p);
    return md * (1.0 + starDepth * cos(starPoints * atan2(d.y, d.x)));
}


float cellSpin(float2 cell) {
    float turns = cellAngle / 360.0;
    turns += spinPhase;
    return -turns * TAU;
}

float smin(float a, float b, float k) {
    if (k <= 0.0) return min(a, b);
    float h = clamp(0.5 + 0.5 * (a - b) / k, 0.0, 1.0);
    return lerp(a, b, h) - k * h * (1.0 - h);
}

void consider(float d, inout float f1, inout float f2) {
    if      (d < f1) f2 = f1;
    else if (d < f2) f2 = d;
    f1 = smin(f1, d, smoothK);
}

float comboValue(float f1, float f2) {
    float s = 1.0 / max(cellRadius, 0.001);
    f1 *= s; f2 *= s;
    float v;
    v = sqrt(max(1.0 - f1 * f1, 0.0));
    v = clamp(v, 0.0, 1.0);
    v = pow(v, edgeSharpen);
    
    return clamp(v, 0.0, 1.0);
}

#define PINCH_R     0.8
#define PINCH_K     0.9
#define PINCH_PULSE 0.5

float2 warpUV(float2 uv) {

    float2  c   = frameC(uv);
    float r   = length(c);
    if (r > 1e-5) {
        float k  = pinchStrength * (1.0 + PINCH_PULSE * sin(warpPhase * TAU));
        float rn = min(r / PINCH_R, 1.0);
        float factor;
        if (k >= 0.0) {
            factor = pow(rn, k * PINCH_K);
        } else {
            factor = 1.0 + (-k) * PINCH_K * (1.0 - rn * rn); 
        }
        c *= factor;
    }
    return frameUV(c);
}

float2 rotateUV(float2 uv) {
    return uv;
}

#define CELL_EMPTY_LO 4.0
#define CELL_EMPTY_HI 7.0
#define CELL_EMPTY_VALUE 1.0

static float2 cBaseMouse;


float cellFinish(float f1, float f2, bool doCells) {
    float v = comboValue(f1, f2);
    if (doCells) v = lerp(v, CELL_EMPTY_VALUE, smoothstep(CELL_EMPTY_LO, CELL_EMPTY_HI, f1));
    
    return v;
}

float cellOrbit(float2 p, float angN, float2 d, bool isBase) {

    float2 ip = floor(p), fp = frac(p);
    float f1 = 8.0, f2 = 8.0;
    for (int j = -2; j <= 2; j++) {
        for (int i = -2; i <= 2; i++) {
            float2 g    = float2(float(i), float(j));
            float2 cell = wrapCell(ip + g, angN, d);
            float2 rnd  = hash2(cell);
            float2 anim = 0.5 + orbitRadius * sin(morphPhase * TAU + rnd * TAU);
            float2 pt   = g + lerp((float2)(0.5), anim, jitter);
            pt += latticeOffset(cell) * (1.0 - jitter);
            
            float d = metricDist(pt - fp, cellSpin(cell));
            
            consider(d, f1, f2);
        }
    }
    
    return cellFinish(f1, f2, false);
}

float cellEval(float2 p, float angN, float2 d, bool isBase) {
    return cellOrbit(p, angN, d, isBase);
}

float cellFBM(float2 p, float angN) {
    float v = 0.0, a = 0.5, norm = 0.0;
    float2  d = loopD;
    int oct = int(octaves + 0.5);
    for (int i = 0; i < MAX_OCTAVES; i++) {
        if (i >= oct) break;
        v    += a * cellEval(p, angN, d, i == 0);
        norm += a;
        p     = p * 2.0 + float2(5.3, 1.7);
        a    *= gain;
        if (angN > 0.5) angN *= 2.0;

        d    *= 2.0;
    }
    return v / norm;
}

float4 shade(float2 screen_coords) {
    float2 block = floor((screen_coords + sampleOffset) / cellSize) * cellSize;
    float2 uv0   = warpUV(block / screenSize);
    float mzAdd = 0.0, valAdd = 0.0;
    float2 uvDisp = (float2)(0.0);

    float2 uv = rotateUV(uv0 + uvDisp);

    float2  p;
    float angN = 0.0;
    {
        p = uv * noiseScale + time * scrollSpeed;
    }
    p.y *= 1.1547;

    float v = cellFBM(p, angN);
    v = clamp(v + valAdd, 0.0, 1.0);

    return paletteLookup(v);
}


            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes IN){
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target {
                float2 px = 1.0 / fwidth(i.uv);
                float ar = px.x / px.y;
                float2 uv = i.uv - 0.5;
                if (ar > 1.77778) uv.x *= ar / 1.77778; else uv.y *= 1.77778 / ar;
                float s = sin(_Rotation), cs = cos(_Rotation);
                uv = mul(uv, float2x2(cs, -s, s, cs));
                uv = uv / _Zoom + _Pan.xy;
                float4 c = shade((uv + 0.5) * float2(640.0, 360.0));
                return c * _Tint;
            }
            ENDHLSL
        }
    }
}