// Leitor do atributo "d" de um path SVG.
//
// Existe porque os glifos passaram a vir prontos do Phosphor em vez de sair de coordenadas
// escritas aqui: o formato deles é este, e alguém tem que traduzir para GraphicsPath.
// Cobre o que os 29 glifos usam -- M, L, H, V, C, Q, A e Z, maiúsculo e minúsculo -- e nada
// além. S e T não aparecem em nenhum deles. Um glifo novo que traga um comando desconhecido
// para o desenho ali mesmo, em vez de continuar cuspindo lixo, e Testes.Icones confere os
// limites de cada path justamente para pegar isso.
using System;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace MoneyControl {

public static class Svg {

    /// <summary>
    /// Lê o "d" e devolve o path com FillMode.Winding, que é a regra nonzero do SVG: onde
    /// duas partes se sobrepõem no mesmo sentido a tinta se soma, e só o sentido invertido
    /// abre furo. É o oposto de Alternate, em que toda sobreposição vira recorte.
    /// </summary>
    public static GraphicsPath Ler(string d) {
        var p = new GraphicsPath(FillMode.Winding);
        int i = 0;
        float cx = 0, cy = 0, sx = 0, sy = 0;   // ponto atual e começo da subfigura
        char cmd = ' ';

        while (true) {
            Pular(d, ref i);
            if (i >= d.Length) break;
            char c = d[i];
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) { cmd = c; i++; }
            else if (cmd == 'M') cmd = 'L';   // par de números repetido depois de um M é lineto
            else if (cmd == 'm') cmd = 'l';
            else if (cmd == ' ') break;       // número solto antes de qualquer comando

            bool rel = cmd >= 'a';
            float ox = rel ? cx : 0, oy = rel ? cy : 0;

            switch (rel ? (char)(cmd - 32) : cmd) {
                case 'M':
                    cx = Num(d, ref i) + ox; cy = Num(d, ref i) + oy;
                    p.StartFigure(); sx = cx; sy = cy; break;

                case 'L': {
                    float x = Num(d, ref i) + ox, y = Num(d, ref i) + oy;
                    p.AddLine(cx, cy, x, y); cx = x; cy = y; break;
                }
                case 'H': {
                    float x = Num(d, ref i) + ox;
                    p.AddLine(cx, cy, x, cy); cx = x; break;
                }
                case 'V': {
                    float y = Num(d, ref i) + oy;
                    p.AddLine(cx, cy, cx, y); cy = y; break;
                }
                case 'C': {
                    float x1 = Num(d, ref i) + ox, y1 = Num(d, ref i) + oy;
                    float x2 = Num(d, ref i) + ox, y2 = Num(d, ref i) + oy;
                    float x = Num(d, ref i) + ox, y = Num(d, ref i) + oy;
                    p.AddBezier(cx, cy, x1, y1, x2, y2, x, y); cx = x; cy = y; break;
                }
                case 'Q': {
                    float qx = Num(d, ref i) + ox, qy = Num(d, ref i) + oy;
                    float x = Num(d, ref i) + ox, y = Num(d, ref i) + oy;
                    // quadrática vira cúbica: os dois controles ficam a 2/3 do caminho
                    // de cada ponta até o ponto de comando
                    p.AddBezier(cx, cy,
                                cx + 2f / 3 * (qx - cx), cy + 2f / 3 * (qy - cy),
                                x + 2f / 3 * (qx - x), y + 2f / 3 * (qy - y), x, y);
                    cx = x; cy = y; break;
                }
                case 'A': {
                    float rx = Num(d, ref i), ry = Num(d, ref i), giro = Num(d, ref i);
                    bool grande = Bandeira(d, ref i), horario = Bandeira(d, ref i);
                    float x = Num(d, ref i) + ox, y = Num(d, ref i) + oy;
                    Arco(p, cx, cy, rx, ry, giro, grande, horario, x, y);
                    cx = x; cy = y; break;
                }
                case 'Z':
                    p.CloseFigure(); cx = sx; cy = sy; break;

                default:
                    return p;   // comando que não sabemos ler: para aqui
            }
        }
        return p;
    }

    static void Pular(string d, ref int i) {
        while (i < d.Length && (d[i] == ' ' || d[i] == ',' || d[i] == '\t' || d[i] == '\r' || d[i] == '\n')) i++;
    }

    static float Num(string d, ref int i) {
        Pular(d, ref i);
        int ini = i;
        if (i < d.Length && (d[i] == '+' || d[i] == '-')) i++;
        while (i < d.Length && ((d[i] >= '0' && d[i] <= '9') || d[i] == '.')) {
            // um segundo ponto já é o número seguinte: "1.5.3" são 1.5 e depois .3
            if (d[i] == '.' && d.IndexOf('.', ini, i - ini) >= 0) break;
            i++;
        }
        if (i < d.Length && (d[i] == 'e' || d[i] == 'E')) {
            i++;
            if (i < d.Length && (d[i] == '+' || d[i] == '-')) i++;
            while (i < d.Length && d[i] >= '0' && d[i] <= '9') i++;
        }
        float v;
        return float.TryParse(d.Substring(ini, i - ini), NumberStyles.Float,
                              CultureInfo.InvariantCulture, out v) ? v : 0;
    }

    /// <summary>
    /// As duas bandeiras do arco são um caractere cada, e a norma deixa colar no número
    /// seguinte: em "0,0,08.4" o segundo zero é bandeira e 8.4 é coordenada. Ler como número
    /// comum engoliria "08.4" inteiro.
    /// </summary>
    static bool Bandeira(string d, ref int i) {
        Pular(d, ref i);
        bool v = i < d.Length && d[i] == '1';
        if (i < d.Length && (d[i] == '0' || d[i] == '1')) i++;
        return v;
    }

    /// <summary>
    /// Arco elíptico aproximado por cúbicas. O SVG descreve o arco pelo ponto de chegada mais
    /// raios, giro e duas bandeiras; o desenho precisa do centro. A conversão é a do apêndice
    /// F.6.5 da especificação, e cada pedaço de até 90° vira uma curva.
    /// </summary>
    static void Arco(GraphicsPath p, float x0f, float y0f, float rxf, float ryf, float giroGraus,
                     bool grande, bool horario, float x1f, float y1f) {
        double x0 = x0f, y0 = y0f, x1 = x1f, y1 = y1f;
        double rx = Math.Abs(rxf), ry = Math.Abs(ryf);
        bool parado = Math.Abs(x1 - x0) < 1e-9 && Math.Abs(y1 - y0) < 1e-9;
        if (rx < 1e-6 || ry < 1e-6 || parado) {
            if (!parado) p.AddLine(x0f, y0f, x1f, y1f);   // raio zero: a norma manda virar reta
            return;
        }

        double fi = giroGraus * Math.PI / 180, cos = Math.Cos(fi), sen = Math.Sin(fi);
        double mx = (x0 - x1) / 2, my = (y0 - y1) / 2;
        double xl = cos * mx + sen * my, yl = -sen * mx + cos * my;

        // raios pequenos demais para alcançar a outra ponta: a norma manda inflar os dois
        double lam = xl * xl / (rx * rx) + yl * yl / (ry * ry);
        if (lam > 1) { double k = Math.Sqrt(lam); rx *= k; ry *= k; }

        double num = rx * rx * ry * ry - rx * rx * yl * yl - ry * ry * xl * xl;
        double den = rx * rx * yl * yl + ry * ry * xl * xl;
        double co = Math.Sqrt(Math.Max(0, num / den));
        if (grande == horario) co = -co;
        double cxl = co * rx * yl / ry, cyl = -co * ry * xl / rx;
        double cx = cos * cxl - sen * cyl + (x0 + x1) / 2;
        double cy = sen * cxl + cos * cyl + (y0 + y1) / 2;

        double t0 = Math.Atan2((yl - cyl) / ry, (xl - cxl) / rx);
        double dt = Math.Atan2((-yl - cyl) / ry, (-xl - cxl) / rx) - t0;
        if (!horario && dt > 0) dt -= 2 * Math.PI;
        if (horario && dt < 0) dt += 2 * Math.PI;

        int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(dt) / (Math.PI / 2)));
        double passo = dt / n, k2 = 4.0 / 3 * Math.Tan(passo / 4);
        double px = x0, py = y0, t = t0;

        for (int seg = 0; seg < n; seg++) {
            double ta = t, tb = t + passo;
            double ax = cx + rx * Math.Cos(ta) * cos - ry * Math.Sin(ta) * sen;
            double ay = cy + rx * Math.Cos(ta) * sen + ry * Math.Sin(ta) * cos;
            double bx = cx + rx * Math.Cos(tb) * cos - ry * Math.Sin(tb) * sen;
            double by = cy + rx * Math.Cos(tb) * sen + ry * Math.Sin(tb) * cos;
            // a tangente em cada ponta dá a direção dos dois controles da curva
            double dax = -rx * Math.Sin(ta) * cos - ry * Math.Cos(ta) * sen;
            double day = -rx * Math.Sin(ta) * sen + ry * Math.Cos(ta) * cos;
            double dbx = -rx * Math.Sin(tb) * cos - ry * Math.Cos(tb) * sen;
            double dby = -rx * Math.Sin(tb) * sen + ry * Math.Cos(tb) * cos;
            p.AddBezier((float)px, (float)py,
                        (float)(ax + k2 * dax), (float)(ay + k2 * day),
                        (float)(bx - k2 * dbx), (float)(by - k2 * dby),
                        (float)bx, (float)by);
            px = bx; py = by; t = tb;
        }
    }
}
}
