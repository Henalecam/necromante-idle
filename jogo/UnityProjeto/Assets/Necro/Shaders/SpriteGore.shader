// Sprite sem iluminação com os efeitos de morte: corte por uma linha, recorte de fragmento,
// dissolução com borda em brasa (fogo), tinta (gelo/veneno/sombra), derretimento e clarão.
// Funciona no pipeline embutido e no URP 2D (passe sem LightMode = SRPDefaultUnlit).
Shader "Necro/SpriteGore"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Cor", Color) = (1,1,1,1)
        _Ruido ("Ruído", 2D) = "gray" {}
        // corte: mantém só um lado da linha (em UV do retângulo do sprite, 0..1)
        _CorteNormal ("Normal do corte (xy) / deslocamento (z) / ligado (w)", Vector) = (0,1,0.5,0)
        // recorte: retângulo em UV local (xmin, ymin, xmax, ymax); usado nos fragmentos
        _Recorte ("Recorte", Vector) = (0,0,1,1)
        _RectSprite ("Retângulo do sprite no atlas (xmin, ymin, largura, altura)", Vector) = (0,0,1,1)
        _Dissolver ("Dissolver", Range(0,1)) = 0
        _CorBrasa ("Cor da borda", Color) = (1,0.45,0.05,1)
        _Tinta ("Tinta", Color) = (0,0,0,0)        // a = intensidade
        _Derreter ("Derreter", Range(0,1)) = 0
        _Clarao ("Clarão", Range(0,1)) = 0
        _CorCarne ("Cor da carne no corte", Color) = (0.35,0.02,0.03,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _Ruido;
            fixed4 _Color, _CorBrasa, _Tinta, _CorCarne;
            float4 _CorteNormal, _Recorte, _RectSprite;
            float _Dissolver, _Derreter, _Clarao;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 luv : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                float2 luv = (v.uv - _RectSprite.xy) / _RectSprite.zw;   // 0..1 dentro do sprite
                // derreter: achata na vertical e alarga a base
                float4 p = v.vertex;
                p.y *= lerp(1.0, 0.12, _Derreter);
                p.x *= lerp(1.0, 1.0 + 0.6 * (1.0 - luv.y), _Derreter);
                o.pos = UnityObjectToClipPos(p);
                o.uv = v.uv;
                o.luv = luv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // derreter: a imagem escorre em colunas
                float escorre = tex2D(_Ruido, float2(i.luv.x * 3.0, 0.3)).r;
                uv.y += _Derreter * escorre * 0.35 * _RectSprite.w * (1.0 - i.luv.y);
                fixed4 c = tex2D(_MainTex, uv) * i.color;

                // recorte de fragmento
                clip(i.luv.x - _Recorte.x); clip(_Recorte.z - i.luv.x);
                clip(i.luv.y - _Recorte.y); clip(_Recorte.w - i.luv.y);

                // corte por linha, com borda irregular e faixa de carne
                if (_CorteNormal.w > 0.5)
                {
                    float r = tex2D(_Ruido, i.luv * 4.0).r - 0.5;
                    float d = dot(i.luv - 0.5, normalize(_CorteNormal.xy)) - (_CorteNormal.z - 0.5) + r * 0.05;
                    clip(d);
                    c.rgb = lerp(c.rgb, _CorCarne.rgb * c.a, step(d, 0.035));
                }

                // dissolver (fogo, sombra): some pelo ruído, com borda brilhante
                float n = tex2D(_Ruido, i.luv * 2.5).r;
                float lim = _Dissolver * 1.1;
                clip(n - lim + 0.0001 - step(1.0, _Dissolver));
                float borda = smoothstep(lim + 0.10, lim, n) * step(0.001, _Dissolver);
                c.rgb = lerp(c.rgb, c.rgb * 0.15, saturate(_Dissolver * 1.5));      // carboniza
                c.rgb += _CorBrasa.rgb * borda * c.a * 2.0;

                c.rgb = lerp(c.rgb, _Tinta.rgb * c.a, _Tinta.a);
                c.rgb = lerp(c.rgb, c.a, _Clarao);
                return c;
            }
            ENDCG
        }
    }
}
