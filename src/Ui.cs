// Tokens e peças visuais do redesenho v5.
//
// WinForms não tem canto arredondado, gradiente, glow nem ícone vetorial. Tudo aqui é
// GDI+ desenhado à mão sobre um único controle — o Card — que as telas ou compõem com
// filhos ou pintam por dentro pelo delegate Desenhar. Um controle só evita a árvore de
// cem Panels aninhados que é o jeito clássico (e piscante) de fazer isso.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MoneyControl {

public static class Ui {
    public static Color H(string s) { return ColorTranslator.FromHtml(s); }

    /* ---------------------------- tokens ---------------------------- */

    public static readonly Color BG    = H("#0A0B0D");   // fundo da janela
    public static readonly Color NAV   = H("#0F1113");   // fundo do menu lateral
    public static readonly Color CARD  = H("#121417");   // cartão / superfície
    public static readonly Color FIELD = H("#15181B");   // campo e hover de linha
    public static readonly Color LINE  = H("#1E2227");   // borda
    public static readonly Color LINE2 = H("#2A2F34");   // borda em hover / ativo
    public static readonly Color DIV   = H("#1B1E22");   // divisória de linha
    public static readonly Color FG    = H("#F2F4F5");
    public static readonly Color FG2   = H("#C3C9CF");
    public static readonly Color FG3   = H("#7D848D");
    public static readonly Color LBL   = H("#6B727B");
    public static readonly Color ACC   = H("#D3FD50");
    public static readonly Color ONACC = H("#0A0B0D");
    public static readonly Color OK    = H("#10B981");
    public static readonly Color WARN  = H("#F59E0B");
    public static readonly Color BAD   = H("#EF4444");
    public static readonly Color ICO   = H("#9AA1A9");   // ícone neutro
    public static readonly Color CHIP  = H("#20241F");   // chip do ícone ativo

    public static readonly Color OKBG   = H("#132019");  // fundos das pílulas de status
    public static readonly Color BADBG  = H("#211A1A");
    public static readonly Color WARNBG = H("#211D14");
    public static readonly Color NEUBG  = H("#15181B");

    public static readonly Color VERDE_BORDA  = H("#2A3320");   // cartão com fatura aberta
    public static readonly Color VERDE_FUNDO  = H("#141A10");
    public static readonly Color RUIM_BORDA   = H("#3A2226");   // cartão de atraso / perigo
    public static readonly Color RUIM_FUNDO   = H("#1A1215");
    public static readonly Color PERIGO_FUNDO = H("#141012");

    public static Color Alfa(Color c, double pct) {
        return Color.FromArgb((int)Math.Round(Math.Max(0, Math.Min(1, pct)) * 255), c);
    }

    /* ---------------------------- tipografia ---------------------------- */

    // Manrope se estiver instalada; senão a fonte do sistema. As espessuras 600/700/800 do
    // redesenho caem todas em Bold: GDI+ só conhece regular e negrito.
    static readonly string FAM = Familia();
    static string Familia() {
        foreach (var n in new[] { "Manrope", "Segoe UI Variable Text", "Segoe UI" }) {
            try { using (var f = new FontFamily(n)) return n; } catch { }
        }
        return "Arial";
    }

    static readonly Dictionary<string, Font> fontes = new Dictionary<string, Font>();
    /// <summary>Fonte em pixels — o redesenho é todo especificado em px.</summary>
    public static Font F(float px, bool negrito = false) {
        string k = px.ToString("F1") + (negrito ? "b" : "r");
        Font f;
        if (fontes.TryGetValue(k, out f)) return f;
        f = new Font(FAM, px, negrito ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        fontes[k] = f;
        return f;
    }

    const TextFormatFlags PADRAO = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                                   TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                                   TextFormatFlags.SingleLine;

    public static void Txt(Graphics g, string s, Font f, Color c, Rectangle r) {
        TextRenderer.DrawText(g, s ?? "", f, r, c, PADRAO);
    }
    public static void TxtDir(Graphics g, string s, Font f, Color c, Rectangle r) {
        TextRenderer.DrawText(g, s ?? "", f, r, c, PADRAO | TextFormatFlags.Right);
    }
    public static void TxtCentro(Graphics g, string s, Font f, Color c, Rectangle r) {
        TextRenderer.DrawText(g, s ?? "", f, r, c, PADRAO | TextFormatFlags.HorizontalCenter);
    }
    public static void TxtQuebra(Graphics g, string s, Font f, Color c, Rectangle r) {
        TextRenderer.DrawText(g, s ?? "", f, r, c,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
    }

    /// <summary>Cifra grande que não pode ser cortada: encolhe a fonte até caber.</summary>
    public static void TxtAjuste(Graphics g, string s, float px, Color c, Rectangle r) {
        var f = F(px, true);
        while (px > 12 && Larg(g, s, f) > r.Width) { px -= 1; f = F(px, true); }
        Txt(g, s, f, c, r);
    }

    public static int Larg(Graphics g, string s, Font f) {
        return TextRenderer.MeasureText(g, s ?? "", f, new Size(int.MaxValue, int.MaxValue),
                                        TextFormatFlags.NoPadding).Width;
    }

    /// <summary>Rótulo 11px em caixa alta com o espaçamento de .06em do redesenho.</summary>
    public static void Rotulo(Graphics g, string s, Color c, int x, int y) {
        var f = F(11, true);
        foreach (char ch in (s ?? "").ToUpper()) {
            string t = ch.ToString();
            TextRenderer.DrawText(g, t, f, new Point(x, y), c, TextFormatFlags.NoPadding);
            x += Larg(g, t, f) + 1;
        }
    }

    /* ---------------------------- formas ---------------------------- */

    public static GraphicsPath Round(RectangleF r, float rad) {
        var p = new GraphicsPath();
        if (r.Width <= 0 || r.Height <= 0) return p;
        rad = Math.Max(0, Math.Min(rad, Math.Min(r.Width, r.Height) / 2f));
        if (rad <= 0.5f) { p.AddRectangle(r); return p; }
        float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, RectangleF r, float rad, Color c) {
        if (r.Width <= 0 || r.Height <= 0 || c.A == 0) return;
        using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
    }

    public static void Borda(Graphics g, RectangleF r, float rad, Color c, float w = 1f) {
        if (r.Width <= w || r.Height <= w || c.A == 0) return;
        var rr = new RectangleF(r.X + w / 2, r.Y + w / 2, r.Width - w, r.Height - w);
        using (var p = Round(rr, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
    }

    /// <summary>Gradiente horizontal — é o do item ativo do menu (acento 13% → 0%).</summary>
    public static void FillGrad(Graphics g, RectangleF r, float rad, Color a, Color b, bool horizontal = true) {
        if (r.Width <= 0 || r.Height <= 0) return;
        using (var p = Round(r, rad))
        using (var br = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2),
                                                a, b, horizontal ? 0f : 90f))
            g.FillPath(br, p);
    }

    /// <summary>
    /// A barra do item ativo, com o glow do redesenho. Não existe box-shadow em GDI+:
    /// são camadas concêntricas cada vez mais transparentes.
    /// </summary>
    public static void BarraAtiva(Graphics g, RectangleF r) {
        for (int i = 8; i >= 1; i--) {
            float e = 20f * i / 8f;
            double a = .55 * (1.0 - (double)i / 9.0) / 6.0;
            Fill(g, RectangleF.Inflate(r, e, e / 2), (r.Width + e * 2) / 2, Alfa(ACC, a));
        }
        Fill(g, r, r.Width / 2, ACC);
    }

    /// <summary>Pílula de status/contagem. Devolve a largura que ocupou.</summary>
    public static int Pilula(Graphics g, string s, Color fg, Color bg, int x, int y, int h = 24, bool negrito = true) {
        var f = F(h >= 24 ? 12 : 11, negrito);
        int w = Larg(g, s, f) + h;
        Fill(g, new RectangleF(x, y, w, h), 99, bg);
        TxtCentro(g, s, f, fg, new Rectangle(x, y, w, h));
        return w;
    }

    /// <summary>Pílula ancorada pela direita — o valor da linha fica logo depois dela.</summary>
    public static int PilulaDir(Graphics g, string s, Color fg, Color bg, int xDireita, int y, int h = 24) {
        var f = F(h >= 24 ? 12 : 11, true);
        int w = Larg(g, s, f) + h;
        Fill(g, new RectangleF(xDireita - w, y, w, h), 99, bg);
        TxtCentro(g, s, f, fg, new Rectangle(xDireita - w, y, w, h));
        return w;
    }

    /// <summary>Barra de progresso: trilho e preenchimento, raio 99.</summary>
    public static void Progresso(Graphics g, Rectangle r, double pct, Color cor, Color? trilho = null) {
        Fill(g, r, r.Height / 2f, trilho ?? LINE);
        double p = Math.Max(0, Math.Min(100, pct));
        if (p <= 0) return;
        int w = (int)Math.Max(r.Height, Math.Round(r.Width * p / 100.0));
        Fill(g, new Rectangle(r.X, r.Y, Math.Min(w, r.Width), r.Height), r.Height / 2f, cor);
    }

    /// <summary>Avatar redondo com as iniciais na cor da pessoa.</summary>
    public static void Avatar(Graphics g, Rectangle r, string nome, Color cor) {
        Fill(g, r, r.Width / 2f, Alfa(cor, .16));
        Borda(g, r, r.Width / 2f, Alfa(cor, .45));
        TxtCentro(g, Iniciais(nome), F(Math.Max(10, r.Height * .38f), true), cor, r);
    }

    public static string Iniciais(string nome) {
        var ws = (nome ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (ws.Length == 0) return "?";
        return (ws[0].Substring(0, 1) + (ws.Length > 1 ? ws[1].Substring(0, 1) : "")).ToUpper();
    }

    /// <summary>Chip quadrado arredondado com o ícone dentro — nunca um ícone solto.</summary>
    public static void Chip(Graphics g, Rectangle r, string icone, Color corIcone, Color fundo, float raio = 12) {
        Fill(g, r, raio, fundo);
        float t = r.Width * .52f;
        Icone(g, icone, new RectangleF(r.X + (r.Width - t) / 2, r.Y + (r.Height - t) / 2, t, t), corIcone);
    }

    /// <summary>Marca vertical de severidade à esquerda de uma linha.</summary>
    public static void Marca(Graphics g, Rectangle r, Color c) { Fill(g, r, r.Width / 2f, c); }

    public static void Divisoria(Graphics g, int x, int y, int w) {
        using (var p = new Pen(DIV)) g.DrawLine(p, x, y, x + w, y);
    }

    /* ---------------------------- ícones ---------------------------- */
    // Glifos sólidos num grid 24×24, desenhados com FillMode.Alternate: o que está dentro
    // de outra forma vira buraco. Sem emoji e sem arquivo de fonte para carregar.

    static GraphicsPath Novo() { var p = new GraphicsPath(); p.FillMode = FillMode.Alternate; return p; }
    static void R(GraphicsPath p, float x, float y, float w, float h, float r) {
        using (var q = Round(new RectangleF(x, y, w, h), r)) if (q.PointCount > 0) p.AddPath(q, false);
    }
    static void E(GraphicsPath p, float x, float y, float w, float h) { p.AddEllipse(x, y, w, h); }
    static void Anel(GraphicsPath p, float x, float y, float w, float h, float esp, float ini, float var) {
        p.AddArc(x, y, w, h, ini, var);
        p.AddArc(x + esp, y + esp, w - 2 * esp, h - 2 * esp, ini + var, -var);
        p.CloseFigure();
    }

    /// <summary>Polígono de cantos arredondados — é o que separa um glifo desenhado de um recorte.</summary>
    static void PgR(GraphicsPath p, float raio, params float[] xy) {
        int n = xy.Length / 2;
        var v = new PointF[n];
        for (int i = 0; i < n; i++) v[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
        var q = new GraphicsPath();
        for (int i = 0; i < n; i++) {
            PointF a = v[(i - 1 + n) % n], b = v[i], c = v[(i + 1) % n];
            // entra e sai do vértice a `raio` de distância, curvando por cima dele
            q.AddBezier(Rumo(b, a, raio), b, b, Rumo(b, c, raio));
        }
        q.CloseFigure();
        p.AddPath(q, false);
        q.Dispose();
    }

    static PointF Rumo(PointF de, PointF para, float d) {
        float dx = para.X - de.X, dy = para.Y - de.Y;
        float m = (float)Math.Sqrt(dx * dx + dy * dy);
        if (m < .001f) return de;
        d = Math.Min(d, m / 2);
        return new PointF(de.X + dx / m * d, de.Y + dy / m * d);
    }

    static void Polar(List<float> xy, double grausCentro, double raio) {
        double a = grausCentro * Math.PI / 180;
        xy.Add((float)(12 + raio * Math.Cos(a)));
        xy.Add((float)(12 + raio * Math.Sin(a)));
    }

    public static void Icone(Graphics g, string nome, RectangleF box, Color cor) {
        var st = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(box.X, box.Y);
        g.ScaleTransform(box.Width / 24f, box.Height / 24f);
        var p = Novo();
        switch (nome ?? "") {
            case "inicio":
                PgR(p, 1.6f, 12, 2, 23, 11.5f, 1, 11.5f); R(p, 4, 11, 16, 11, 3); R(p, 10, 15, 4, 7, 1); break;
            case "cartao":
                R(p, 2, 5, 20, 14, 4); R(p, 3.5f, 9, 17, 3, 0); R(p, 5, 14, 6, 2, 1); break;
            case "carteira":
                R(p, 2, 5, 20, 15, 4); E(p, 15, 10, 5, 5); break;
            case "resumo":
                R(p, 3, 13, 4, 8, 1.5f); R(p, 10, 7, 4, 14, 1.5f); R(p, 17, 3, 4, 18, 1.5f); break;
            case "calendario":
                R(p, 7, 2, 2, 5, 1); R(p, 15, 2, 2, 5, 1); R(p, 3, 5, 18, 17, 4);
                R(p, 4.5f, 10, 15, 1.6f, 0); E(p, 7, 14, 3, 3); E(p, 11, 14, 3, 3); E(p, 15, 14, 3, 3); break;
            case "parcelas":
                R(p, 2, 4, 5, 3, 1.5f); R(p, 9, 4, 13, 3, 1.5f);
                R(p, 2, 10.5f, 5, 3, 1.5f); R(p, 9, 10.5f, 13, 3, 1.5f);
                R(p, 2, 17, 5, 3, 1.5f); R(p, 9, 17, 13, 3, 1.5f); break;
            case "compras":   // carrinho: a sacola vira um borrão num chip de 18px, o carrinho não
                // formas que não se cruzam: com FillMode.Alternate, sobreposição viraria buraco
                PgR(p, 0.7f, 0.8f, 2.4f, 6.6f, 2.4f, 9, 6, 6.2f, 6, 4.4f, 4.8f, 0.8f, 4.8f);
                PgR(p, 1.2f, 6.8f, 6, 22.5f, 6, 19.5f, 15, 9, 15);
                E(p, 7.6f, 17, 3.8f, 3.8f); E(p, 15.6f, 17, 3.8f, 3.8f); break;
            case "pessoas":
                E(p, 4, 3, 8, 8); R(p, 1, 13, 14, 9, 4.5f); E(p, 14, 5, 6, 6); R(p, 16, 14, 7, 8, 3.5f); break;
            case "pessoa":
                E(p, 8, 3, 8, 8); R(p, 3, 14, 18, 8, 4); break;
            case "config":
                E(p, 3, 3, 18, 18); E(p, 8.5f, 8.5f, 7, 7);
                R(p, 10.5f, 0, 3, 4, 1); R(p, 10.5f, 20, 3, 4, 1);
                R(p, 0, 10.5f, 4, 3, 1); R(p, 20, 10.5f, 4, 3, 1); break;
            case "backup":
                PgR(p, 1.8f, 12, 2, 21, 6, 21, 12, 12, 22, 3, 12, 3, 6);
                PgR(p, 0.6f, 7.5f, 12, 10.5f, 15, 16, 8.5f, 18, 10.5f, 10.5f, 18.5f, 6, 14); break;
            case "alerta":
                PgR(p, 2.2f, 12, 2.5f, 23, 21, 1, 21); R(p, 11, 9, 2, 6, 1); E(p, 11, 16.5f, 2, 2); break;
            case "check":
                PgR(p, 1f, 2.5f, 12, 5.5f, 9, 9.5f, 13, 18.5f, 4, 21.5f, 7, 9.5f, 19); break;
            case "relogio":
                E(p, 2, 2, 20, 20); E(p, 5, 5, 14, 14); R(p, 11, 6.5f, 2, 6.5f, 1); R(p, 11, 11, 6, 2, 1); break;
            case "cobrar":  // o rabicho encosta na base do balão, não entra: sobreposição virava buraco
                R(p, 2, 2, 20, 16, 5); PgR(p, .9f, 7, 18, 7, 23, 13, 18);
                R(p, 6, 6.5f, 12, 2, 1); R(p, 6, 11, 8, 2, 1); break;
            case "mais":
                R(p, 10.5f, 3, 3, 18, 1.5f); R(p, 3, 10.5f, 18, 3, 1.5f); break;
            case "editar":
                PgR(p, .8f, 2, 22, 3.6f, 16.8f, 14.6f, 5.8f, 19.2f, 10.4f, 8.2f, 21.4f);
                PgR(p, .6f, 16.4f, 4, 20, 7.6f, 22, 5.6f, 18.4f, 2); break;
            case "excluir":
                R(p, 3, 5, 18, 2.6f, 1.3f); R(p, 9, 2, 6, 3, 1); R(p, 10.5f, 3.2f, 3, 1.8f, .6f);
                R(p, 5, 8.5f, 14, 13.5f, 3); R(p, 9, 11.5f, 2, 7.5f, 1); R(p, 13, 11.5f, 2, 7.5f, 1); break;
            case "seta":
                PgR(p, 1.2f, 4, 10.5f, 13.5f, 10.5f, 13.5f, 5, 21, 12, 13.5f, 19, 13.5f, 13.5f, 4, 13.5f); break;
            case "fechar":  // contorno único do X: duas barras cruzadas viravam gravata-borboleta
                PgR(p, 1.3f, 21.3f, 18.7f, 14.7f, 12, 21.3f, 5.3f, 18.7f, 2.7f, 12, 9.3f,
                    5.3f, 2.7f, 2.7f, 5.3f, 9.3f, 12, 2.7f, 18.7f, 5.3f, 21.3f, 12, 14.7f,
                    18.7f, 21.3f); break;
            case "dinheiro":
                R(p, 2, 5, 20, 14, 3); E(p, 9.5f, 8.5f, 5, 7); break;
            case "exportar":
                R(p, 10.5f, 2, 3, 9, 1.5f); PgR(p, 1.2f, 6.5f, 11, 17.5f, 11, 12, 18);
                R(p, 3, 19.5f, 18, 2.6f, 1.3f); break;
            case "garfo":
                R(p, 4, 2, 1.6f, 9, .8f); R(p, 7, 2, 1.6f, 9, .8f); R(p, 10, 2, 1.6f, 9, .8f);
                R(p, 6.2f, 9.5f, 3, 12.5f, 1.5f); PgR(p, 1f, 16, 2, 19, 2, 19, 13, 16, 13);
                R(p, 16.8f, 13, 1.6f, 9, .8f); break;
            case "monitor":
                R(p, 2, 3, 20, 14, 3); R(p, 5, 6, 14, 8, 1); R(p, 10.5f, 17.5f, 3, 2.5f, 0);
                R(p, 6, 20, 12, 2.2f, 1.1f); break;
            case "camisa":
                PgR(p, .8f, 9, 2, 12, 4.5f, 15, 2, 21, 5, 19, 10.5f, 17, 9.5f, 17, 22, 7, 22, 7, 9.5f, 5, 10.5f, 3, 5); break;
            case "carro":
                R(p, 2, 10, 20, 8, 3); PgR(p, 1.2f, 5, 10, 7, 4.5f, 17, 4.5f, 19, 10);
                E(p, 4.5f, 13.5f, 4, 4); E(p, 15.5f, 13.5f, 4, 4); break;
            case "repete":
                Anel(p, 3, 3, 18, 18, 2.8f, 50, 280); PgR(p, 1f, 17.5f, 0.5f, 23.5f, 5.5f, 16, 7.5f); break;
            case "estrela": {
                var xy = new List<float>();
                for (int i = 0; i < 10; i++) Polar(xy, -90 + i * 36, i % 2 == 0 ? 10.0 : 4.4);
                PgR(p, 1.5f, xy.ToArray()); break;
            }
            default:  // "pontos"
                E(p, 2.5f, 10.5f, 3.5f, 3.5f); E(p, 10.25f, 10.5f, 3.5f, 3.5f); E(p, 18, 10.5f, 3.5f, 3.5f); break;
        }
        using (var b = new SolidBrush(cor)) g.FillPath(b, p);
        p.Dispose();
        g.Restore(st);
    }

    /* ---------------------------- barra de título ---------------------------- */

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr janela, int atributo, ref int valor, int tamanho);

    /// <summary>
    /// Manda o Windows desenhar a barra de título no escuro, independente do tema do sistema:
    /// o app é escuro sempre. Quem pinta continua sendo o DWM — trocar uma cor não vale
    /// reimplementar arrastar, encaixar, maximizar e redimensionar na mão.
    /// </summary>
    public static void BarraEscura(Form f) {
        try {
            int sim = 1;
            // 20 desde a build 18985; 19 nas anteriores
            if (DwmSetWindowAttribute(f.Handle, 20, ref sim, 4) != 0)
                DwmSetWindowAttribute(f.Handle, 19, ref sim, 4);
        } catch { }
    }

    /* ---------------------------- a marca ---------------------------- */

    /// <summary>
    /// O cartão holográfico sobre o quadrado escuro, desenhado num grid de 100 — é a mesma
    /// rotina que pinta o chip do menu e que gera o .ico do executável, então os dois nunca
    /// saem diferentes. O degradê real é uma malha 2D; aqui são duas passadas cruzadas.
    /// </summary>
    public static void Marca(Graphics g, RectangleF box, bool fundo = true) {
        var st = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(box.X, box.Y);
        g.ScaleTransform(box.Width / 100f, box.Height / 100f);

        if (fundo) {
            using (var p = Round(new RectangleF(0, 0, 100, 100), 22.5f))
            using (var b = new LinearGradientBrush(new RectangleF(-1, -1, 102, 102),
                                                   H("#26282C"), H("#17181A"), 90f))
                g.FillPath(b, p);
        }

        Brilho(g, 74.5f, 25f, 4.2f, 9f);

        var st2 = g.Save();
        g.TranslateTransform(49, 49);
        g.RotateTransform(-9);
        var cartao = new RectangleF(-27, -16.5f, 54, 33);
        var moldura = new RectangleF(-28, -17.5f, 56, 35);
        using (var p = Round(cartao, 4.5f)) {
            using (var b = new LinearGradientBrush(moldura, Color.White, Color.White, 20f)) {
                b.InterpolationColors = new ColorBlend {
                    Colors = new[] { H("#A9DCF5"), H("#CFDCF4"), H("#F6C4DE"), H("#FBE4A0") },
                    Positions = new[] { 0f, .26f, .6f, 1f },
                };
                g.FillPath(b, p);
            }
            // segunda passada: o verde sobe do canto de baixo à esquerda e some no meio
            using (var b = new LinearGradientBrush(moldura, Color.White, Color.White, -42f)) {
                var verde = H("#C2E7B6");
                b.InterpolationColors = new ColorBlend {
                    Colors = new[] { Alfa(verde, .92), Alfa(verde, .55), Alfa(verde, 0), Alfa(verde, 0) },
                    Positions = new[] { 0f, .22f, .55f, 1f },
                };
                g.FillPath(b, p);
            }
            using (var caneta = new Pen(Alfa(Color.White, .55), .9f)) g.DrawPath(caneta, p);
        }

        // chip dourado
        var chip = new RectangleF(-17.5f, -6f, 10.5f, 9f);
        Fill(g, chip, 2f, H("#E9C36F"));
        using (var risco = new Pen(H("#C79F52"), .8f)) {
            g.DrawLine(risco, chip.X + chip.Width / 2, chip.Y, chip.X + chip.Width / 2, chip.Bottom);
            g.DrawLine(risco, chip.X, chip.Y + chip.Height / 2, chip.Right, chip.Y + chip.Height / 2);
        }

        var barra = Color.FromArgb(190, 166, 188, 166);
        Fill(g, new RectangleF(-17.5f, 6f, 27.5f, 3f), 1.5f, barra);
        Fill(g, new RectangleF(12.8f, 5.4f, 8.4f, 3f), 1.5f, barra);

        g.Restore(st2);
        g.Restore(st);
    }

    /// <summary>Estrela de quatro pontas com os lados côncavos.</summary>
    static void Brilho(Graphics g, float cx, float cy, float rx, float ry) {
        var p = new GraphicsPath();
        float k = .15f;   // quanto o lado afunda em direção ao centro
        p.AddBezier(cx, cy - ry, cx + rx * k, cy - ry * k, cx + rx * k, cy - ry * k, cx + rx, cy);
        p.AddBezier(cx + rx, cy, cx + rx * k, cy + ry * k, cx + rx * k, cy + ry * k, cx, cy + ry);
        p.AddBezier(cx, cy + ry, cx - rx * k, cy + ry * k, cx - rx * k, cy + ry * k, cx - rx, cy);
        p.AddBezier(cx - rx, cy, cx - rx * k, cy - ry * k, cx - rx * k, cy - ry * k, cx, cy - ry);
        p.CloseFigure();
        using (var b = new SolidBrush(Color.White)) g.FillPath(b, p);
        p.Dispose();
    }

    static readonly Dictionary<string, string> ICONE_CAT = new Dictionary<string, string> {
        { "Alimentação", "garfo" }, { "Mercado", "compras" }, { "Eletrônicos", "monitor" },
        { "Roupas", "camisa" }, { "Transporte", "carro" }, { "Assinaturas", "repete" },
        { "Lazer", "estrela" }, { "Outros", "pontos" },
    };
    public static string IconeCategoria(string cat) {
        string i;
        return ICONE_CAT.TryGetValue(cat ?? "", out i) ? i : "pontos";
    }

    /* ---------------------------- item do menu lateral ---------------------------- */

    public const int MENU_W = 268;

    public static Card ItemMenu(string icone, string texto, string sub, Color subCor,
                                string badge, Color badgeCor, bool ativo, Action ir) {
        var c = new Card {
            Width = MENU_W, Height = sub != null ? 60 : 50, Raio = 14,
            BackColor = NAV, Fora = new Padding(14, 3, 14, 3),
            Fundo = Color.Transparent, Borda = Color.Transparent, BarraAtiva = ativo,
        };
        if (ativo) { c.Fundo = Alfa(ACC, .13); c.Grad = Alfa(ACC, 0); c.Borda = LINE2; }
        c.Desenhar = (g, r) => {
            var chip = new Rectangle(r.X + 10, r.Y + (r.Height - 34) / 2, 34, 34);
            Chip(g, chip, icone, ativo ? ACC : ICO, ativo ? CHIP : FIELD, 11);
            int bw = 0;
            if (!string.IsNullOrEmpty(badge)) {
                var f = F(11, true);
                bw = Larg(g, badge, f) + 18;
                Fill(g, new Rectangle(r.Right - 12 - bw, r.Y + (r.Height - 20) / 2, bw, 20), 99,
                     badgeCor == BAD ? BADBG : NEUBG);
                TxtCentro(g, badge, f, badgeCor,
                          new Rectangle(r.Right - 12 - bw, r.Y + (r.Height - 20) / 2, bw, 20));
                bw += 8;
            }
            int tx = chip.Right + 12, tw = r.Right - 12 - bw - tx;
            if (sub == null) {
                Txt(g, texto, F(14, ativo), ativo ? FG : FG2, new Rectangle(tx, r.Y, tw, r.Height));
            } else {
                Txt(g, texto, F(14, ativo), ativo ? FG : FG2, new Rectangle(tx, r.Y + 10, tw, 18));
                Txt(g, sub, F(12, true), subCor, new Rectangle(tx, r.Y + 30, tw, 16));
            }
        };
        c.Clicavel(ir);
        return c;
    }

    public static Control RotuloMenu(string s) {
        var c = new Card {
            Width = MENU_W, Height = 34, BackColor = NAV,
            Fundo = Color.Transparent, Borda = Color.Transparent,
        };
        c.Desenhar = (g, r) => Rotulo(g, s, LBL, r.X + 24, r.Y + 14);
        return c;
    }
}

/* ============================== controles ============================== */

/// <summary>
/// Superfície arredondada. Ou recebe filhos (botões, campos) ou se pinta inteira pelo
/// delegate Desenhar — em linha de lista, pintar sai muito mais barato que montar
/// oito Labels que ainda teriam que combinar hover entre si.
/// </summary>
public class Card : Panel {
    public int Raio = 20;
    public Color Fundo = Ui.CARD, Borda = Ui.LINE;
    public Color? Grad;                              // gradiente horizontal Fundo → Grad
    public Padding Fora = Padding.Empty;             // recuo da superfície dentro do controle
    public bool Hover, BarraAtiva;
    public Action<Graphics, Rectangle> Desenhar;     // conteúdo pintado à mão (rect já sem Padding)
    public Action Clique;
    public Action<Point> CliqueEm;                   // ponto relativo ao rect de Desenhar

    bool dentro;

    public Card() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Ui.BG;
    }

    public Card Clicavel(Action a) {
        Clique = a; Hover = true; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        return this;
    }

    /// <summary>Filhos participam do hover e do clique do cartão — senão o cartão "apaga" sob eles.</summary>
    public void Adotar(Control c) {
        c.MouseEnter += (o, e) => { dentro = true; Invalidate(); };
        c.MouseLeave += (o, e) => { dentro = false; Invalidate(); };
        if (Clique != null && !(c is Button)) {
            c.Cursor = Cursors.Hand;
            c.Click += (o, e) => Clique();
        }
        foreach (Control n in c.Controls) Adotar(n);
    }

    protected override bool IsInputKey(Keys k) {
        return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k);
    }
    protected override void OnMouseEnter(EventArgs e) { dentro = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { dentro = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnMouseDown(MouseEventArgs e) {
        if (TabStop) Focus();
        base.OnMouseDown(e);
    }
    protected override void OnMouseClick(MouseEventArgs e) {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        if (CliqueEm != null) CliqueEm(new Point(e.X - Fora.Left, e.Y - Fora.Top));
        else if (Clique != null) Clique();
    }
    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) return;
        if (Clique != null) Clique();
        else if (CliqueEm != null) CliqueEm(new Point(0, 0));
    }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new Rectangle(Fora.Left, Fora.Top, Width - Fora.Horizontal, Height - Fora.Vertical);
        if (r.Width <= 0 || r.Height <= 0) { base.OnPaint(e); return; }

        // regra do redesenho: qualquer linha ou cartão clicável muda fundo e borda no hover
        bool realce = dentro && Hover;
        if (Grad.HasValue) Ui.FillGrad(g, r, Raio, realce ? Ui.FIELD : Fundo, Grad.Value);
        else Ui.Fill(g, r, Raio, realce ? Ui.FIELD : Fundo);
        Ui.Borda(g, r, Raio, realce ? Ui.LINE2 : Borda);

        if (BarraAtiva) Ui.BarraAtiva(g, new RectangleF(0, r.Y + 9, 5, r.Height - 18));
        if (Focused && TabStop) Ui.Borda(g, Rectangle.Inflate(r, -1, -1), Raio, Ui.ACC, 2);

        if (Desenhar != null) Desenhar(g, r);
        base.OnPaint(e);
    }
}

/// <summary>Botão do redesenho: pílula ou raio 13, com chip de ícone opcional.</summary>
public class Botao : Button {
    public bool Primario, Pilula, Ativo, Perigo;
    public string Icone;
    bool dentro;

    public Botao(string txt, string icone = null, bool primario = false) {
        Icone = icone; Primario = primario;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Ui.BG;
        Font = Ui.F(14, true);
        AutoSize = false;
        Height = 40;
        Text = txt;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
    }

    public override string Text {
        get { return base.Text; }
        set { base.Text = value; Width = Medida(); }
    }

    int Medida() {
        int t = TextRenderer.MeasureText(base.Text ?? "", Font ?? Ui.F(14, true),
                                        new Size(int.MaxValue, 40), TextFormatFlags.NoPadding).Width;
        return t + 34 + (Icone != null ? 26 : 0);
    }

    /// <summary>Ajusta a largura depois de trocar fonte ou ícone.</summary>
    public Botao Medir() { Width = Medida(); return this; }

    protected override void OnMouseEnter(EventArgs e) { dentro = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { dentro = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        bool cheio = Primario || Ativo;

        Ui.Fill(g, r, Pilula ? 99 : 13, cheio ? Ui.ACC : (dentro ? Ui.LINE2 : Ui.FIELD));
        Ui.Borda(g, r, Pilula ? 99 : 13, cheio ? Ui.ACC : (dentro ? Ui.LINE2 : Ui.LINE));
        if (Focused) Ui.Borda(g, Rectangle.Inflate(r, -1, -1), Pilula ? 99 : 13, cheio ? Ui.ONACC : Ui.ACC, 2);

        var cor = cheio ? Ui.ONACC : Perigo ? Ui.BAD : Ui.FG;
        int x = 16;
        if (Icone != null) {
            Ui.Icone(g, Icone, new RectangleF(x, (Height - 17) / 2f, 17, 17), cor);
            x += 26;
        }
        Ui.Txt(g, base.Text, Font, cor, new Rectangle(x, 0, Width - x - 14, Height));
    }
}

}
