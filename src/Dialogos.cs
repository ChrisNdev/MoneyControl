// Caixas de cadastro. Compra do cartão e dívida pessoal usam o mesmo formulário:
// muda o rótulo de "quem" e pouco mais — duas telas iguais seriam dois lugares pra errar.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace MoneyControl {

/// <summary>
/// Janela modal do redesenho: sem barra do Windows, cantos de 24px e um backdrop escuro
/// por cima da janela de trás. O backdrop é outra Form translúcida — é o único jeito de
/// escurecer o que está atrás sem redesenhar a janela principal inteira.
/// </summary>
public class Dialogo : Form {
    /// <summary>Área útil do corpo, já recuada 26px das bordas: é nela que os campos entram.</summary>
    protected readonly Panel Corpo = new Panel();
    protected Botao Ok, Cancelar;
    readonly Panel fora = new Panel();
    readonly Card cab, rod;
    readonly int larguraDialogo;
    Point arrasto;
    bool arrastando;

    public Dialogo(string icone, string titulo, string sub, int largura, int alturaCorpo, string acao) {
        larguraDialogo = largura;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Ui.CARD; ForeColor = Ui.FG; Font = Ui.F(14);
        ClientSize = new Size(largura, 96 + alturaCorpo + 84);
        using (var p = Ui.Round(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 24))
            Region = new Region(p);

        cab = new Card {
            Dock = DockStyle.Top, Height = 96, BackColor = Ui.CARD,
            Fundo = Color.Transparent, Borda = Color.Transparent,
        };
        cab.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 26, r.Y + 26, 42, 42), icone, Ui.ACC, Ui.CHIP, 13);
            Ui.Txt(g, titulo, Ui.F(17, true), Ui.FG, new Rectangle(r.X + 80, r.Y + 28, r.Width - 140, 22));
            Ui.Txt(g, sub, Ui.F(12), Ui.FG3, new Rectangle(r.X + 80, r.Y + 50, r.Width - 140, 18));
            Ui.Divisoria(g, r.X + 26, r.Bottom - 1, r.Width - 52);
        };
        cab.MouseDown += (o, e) => { arrastando = true; arrasto = e.Location; };
        cab.MouseUp += (o, e) => arrastando = false;
        cab.MouseMove += (o, e) => {
            if (arrastando) Location = new Point(Location.X + e.X - arrasto.X, Location.Y + e.Y - arrasto.Y);
        };

        var fechar = new Botao("", "fechar") { Height = 34, Width = 34, BackColor = Ui.CARD };
        fechar.Location = new Point(largura - 26 - 34, 30);
        fechar.Click += (o, e) => { DialogResult = DialogResult.Cancel; Close(); };
        cab.Controls.Add(fechar);

        rod = new Card {
            Dock = DockStyle.Bottom, Height = 84, BackColor = Ui.CARD,
            Fundo = Color.Transparent, Borda = Color.Transparent,
        };
        rod.Desenhar = (g, r) => Ui.Divisoria(g, r.X + 26, r.Y, r.Width - 52);

        Ok = new Botao(acao, "check", true) { BackColor = Ui.CARD };
        Ok.Location = new Point(largura - 26 - Ok.Width, 22);
        Cancelar = new Botao("Cancelar") { BackColor = Ui.CARD, DialogResult = DialogResult.Cancel };
        Cancelar.Location = new Point(largura - 26 - Ok.Width - 10 - Cancelar.Width, 22);
        rod.Controls.Add(Ok); rod.Controls.Add(Cancelar);

        fora.Dock = DockStyle.Fill;
        fora.BackColor = Ui.CARD;
        Corpo.Location = new Point(26, 10);
        Corpo.Size = new Size(largura - 52, alturaCorpo);
        Corpo.BackColor = Ui.CARD;
        fora.Controls.Add(Corpo);

        Controls.Add(fora);
        Controls.Add(rod);
        Controls.Add(cab);

        AcceptButton = Ok;
        CancelButton = Cancelar;
        KeyPreview = true;
    }

    /// <summary>Põe um controle no rodapé, à esquerda de Cancelar/Salvar.</summary>
    protected void Rodape(Control c) { rod.Controls.Add(c); }

    /// <summary>Refaz a altura depois que o corpo se mediu sozinho (as pílulas quebram linha).</summary>
    protected void Altura(int alturaCorpo) {
        Corpo.Height = alturaCorpo;
        ClientSize = new Size(larguraDialogo, 96 + alturaCorpo + 84);
        using (var p = Ui.Round(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), 24))
            Region = new Region(p);
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Ui.Borda(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), 24, Ui.LINE2);
    }

    /// <summary>Abre com o backdrop por cima da janela de trás.</summary>
    public new DialogResult ShowDialog(IWin32Window dono) {
        var pai = dono as Form;
        if (pai == null) return base.ShowDialog(dono);
        using (var fundo = new Form {
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false, BackColor = Ui.H("#060709"),
            Opacity = .72, StartPosition = FormStartPosition.Manual, Bounds = pai.Bounds, Owner = pai,
        }) {
            fundo.Show();
            var r = base.ShowDialog(fundo);
            fundo.Hide();
            return r;
        }
    }

    /* peças reaproveitadas pelos formulários */

    protected Card Rot(string s, int x, int y, int w) {
        var c = new Card {
            Location = new Point(x, y), Size = new Size(w, 16), BackColor = Ui.CARD,
            Fundo = Color.Transparent, Borda = Color.Transparent,
        };
        c.Desenhar = (g, r) => Ui.Rotulo(g, s, Ui.LBL, r.X, r.Y + 2);
        Corpo.Controls.Add(c);
        return c;
    }

    /// <summary>Campo: WinForms não arredonda um TextBox, então ele mora dentro de um cartão.</summary>
    protected Card Campo(Control interno, int x, int y, int w, int h = 44) {
        var c = new Card {
            Location = new Point(x, y), Size = new Size(w, h), Raio = 14, BackColor = Ui.CARD,
            Fundo = Ui.FIELD, Borda = Ui.LINE,
        };
        interno.BackColor = Ui.FIELD;
        interno.ForeColor = Ui.FG;
        interno.Font = Ui.F(15);

        var caixa = interno as TextBoxBase;
        if (caixa != null) caixa.BorderStyle = BorderStyle.None;
        var num = interno as NumericUpDown;
        // as setinhas são desenhadas pelo Windows e ficam brancas no escuro
        if (num != null) { num.BorderStyle = BorderStyle.None; num.Controls[0].Visible = false; }
        var combo = interno as ComboBox;
        if (combo != null) {
            // sem owner-draw a lista sai clara mesmo com FlatStyle e BackColor definidos
            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 22;
            combo.DrawItem += (o, e) => {
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(sel ? Ui.LINE2 : Ui.FIELD)) e.Graphics.FillRectangle(b, e.Bounds);
                if (e.Index < 0) return;
                TextRenderer.DrawText(e.Graphics, combo.Items[e.Index].ToString(), Ui.F(15),
                    new Rectangle(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height),
                    Ui.FG, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
        }

        interno.Location = new Point(12, (h - interno.Height) / 2);
        interno.Width = w - 24;
        c.Controls.Add(interno);
        Corpo.Controls.Add(c);
        return c;
    }

    protected static MaskedTextBox Data(string mascara) {
        return new MaskedTextBox {
            Mask = mascara, BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Left,
            PromptChar = '_', Height = 22,
        };
    }

    protected static string Ymd(MaskedTextBox m) {
        DateTime d;
        if (!DateTime.TryParseExact(m.Text, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out d)) return null;
        return d.ToString("yyyy-MM-dd");
    }

    protected static string Ym(MaskedTextBox m) {
        DateTime d;
        if (!DateTime.TryParseExact(m.Text, "MM/yyyy", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out d)) return null;
        return d.ToString("yyyy-MM");
    }
}

/* ============================== lançamento ============================== */

public class FormLanc : Dialogo {
    public Lanc Resultado;

    readonly bool cartao;
    readonly Estado S;
    readonly Lanc original;

    readonly TextBox credor = new TextBox();
    readonly TextBox nome = new TextBox();
    readonly NumericUpDown total = new NumericUpDown();
    readonly NumericUpDown n = new NumericUpDown();
    readonly MaskedTextBox ate = Data("00/0000");
    readonly MaskedTextBox data = Data("00/00/0000");
    readonly MaskedTextBox first = Data("00/00/0000");
    readonly Card previa;
    readonly Card interruptor;
    readonly Card rotN, rotAte, campoN, campoAte, rotTotal, rotFirst;
    readonly List<Botao> pilulas = new List<Botao>();
    readonly List<Botao> pessoas = new List<Botao>();

    string cat = "Outros";
    string pid = "";
    bool rec;

    const int M = 508, COL = 246;

    public FormLanc(Estado s, bool cartao, Lanc edit)
        : base(cartao ? "compras" : "carteira",
               (edit != null ? "Editar " : "Nova ") + (cartao ? "compra" : "dívida"),
               cartao ? "Vai virar parcelas na fatura de quem usou o cartão."
                      : "Vai virar parcelas com vencimento mês a mês.",
               560, 468, "Salvar") {
        S = s; this.cartao = cartao; original = edit;

        // linha explicada do interruptor de assinatura
        interruptor = new Card {
            Location = new Point(0, 0), Size = new Size(M, 62), Raio = 16, BackColor = Ui.CARD,
            Fundo = Ui.FIELD, Borda = Ui.LINE,
        };
        interruptor.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 14, r.Y + 15, 32, 32), "repete", rec ? Ui.ACC : Ui.ICO,
                    rec ? Ui.CHIP : Ui.NEUBG, 11);
            Ui.Txt(g, "É uma assinatura", Ui.F(14, true), Ui.FG, new Rectangle(r.X + 56, r.Y + 12, 300, 20));
            Ui.Txt(g, rec ? "repete todo mês, sem fim — o valor é o de cada mês"
                          : "não repete: é um valor total dividido em parcelas",
                   Ui.F(12), Ui.FG3, new Rectangle(r.X + 56, r.Y + 32, 380, 18));
            var trilho = new Rectangle(r.Right - 68, r.Y + 20, 46, 24);
            Ui.Fill(g, trilho, 12, rec ? Ui.ACC : Ui.LINE2);
            Ui.Fill(g, new Rectangle(rec ? trilho.Right - 21 : trilho.X + 3, trilho.Y + 3, 18, 18), 9,
                    rec ? Ui.ONACC : Ui.FG3);
        };
        interruptor.Clicavel(() => { rec = !rec; Sincronizar(); });
        Corpo.Controls.Add(interruptor);

        total.DecimalPlaces = 2; total.Maximum = 9999999; total.Increment = 10;
        n.Minimum = 1; n.Maximum = cartao ? 72 : 360; n.Value = 1;

        int y = 74;
        Rot(cartao ? "Pessoa" : "Para quem eu devo", 0, y, M);
        y += 18;
        if (cartao) {
            // pílulas em vez de combo: um ComboBox do Windows sai claro no meio do escuro
            int linha = y;
            foreach (var p in S.people) {
                var dessa = p;
                var b = new Botao(p.nome) {
                    Pilula = true, Height = 38, BackColor = Ui.CARD, Font = Ui.F(13, true),
                };
                b.Medir();
                linha = Fila(pessoas, b, M, linha, 46);
                b.Click += (o, e) => { pid = dessa.id; MarcarPessoas(); };
                Corpo.Controls.Add(b);
            }
            y = linha + 50;
        } else {
            // credor não tem cadastro, só nome: os já usados viram sugestão
            credor.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            credor.AutoCompleteSource = AutoCompleteSource.CustomSource;
            var fonte = new AutoCompleteStringCollection();
            foreach (var c in Calc.ColunasDividas(S)) fonte.Add(c.nome);
            credor.AutoCompleteCustomSource = fonte;
            Campo(credor, 0, y, M);
            y += 52;
        }

        Rot("O que foi", 0, y, M);
        Campo(nome, 0, y + 18, M);
        y += 78;

        rotTotal = Rot("Valor total (R$)", 0, y, COL);
        rotN = Rot("Parcelas", COL + 16, y, COL);
        rotAte = Rot("Cobrar até (mm/aaaa, vazio = sem fim)", COL + 16, y, COL);
        Campo(total, 0, y + 18, COL);
        campoN = Campo(n, COL + 16, y + 18, COL);
        campoAte = Campo(ate, COL + 16, y + 18, COL);
        y += 78;

        Rot(cartao ? "Data da compra" : "Quando assumi", 0, y, COL);
        rotFirst = Rot("1ª parcela vence", COL + 16, y, COL);
        Campo(data, 0, y + 18, COL);
        Campo(first, COL + 16, y + 18, COL);
        y += 80;

        Rot("Categoria", 0, y, M);
        int py = y + 20;
        foreach (var c in Calc.CATS) {
            string dessa = c;
            var b = new Botao(dessa, Ui.IconeCategoria(dessa)) {
                Pilula = true, Height = 36, BackColor = Ui.CARD, Font = Ui.F(13, true),
            };
            b.Medir();
            py = Fila(pilulas, b, M, py, 44);
            b.Click += (o, e) => { cat = dessa; MarcarPilulas(); };
            Corpo.Controls.Add(b);
        }

        // faixa de conferência: mostra em texto o que o cálculo vai gerar, a cada tecla
        previa = new Card {
            Location = new Point(0, py + 50), Size = new Size(M, 56), Raio = 16, BackColor = Ui.CARD,
            Fundo = Ui.NEUBG, Borda = Ui.LINE,
        };
        Altura(py + 50 + 56 + 6);
        previa.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 14, r.Y + 14, 28, 28), "check", Ui.ACC, Ui.CHIP, 10);
            Ui.Rotulo(g, "Vai gerar", Ui.LBL, r.X + 52, r.Y + 12);
            Ui.Txt(g, Conferencia(), Ui.F(13, true), Ui.FG2, new Rectangle(r.X + 52, r.Y + 28, r.Width - 66, 20));
        };
        Corpo.Controls.Add(previa);

        EventHandler mudou = (o, e) => previa.Invalidate();
        total.ValueChanged += mudou;
        n.ValueChanged += mudou;
        ate.TextChanged += mudou;
        first.TextChanged += mudou;
        data.TextChanged += (o, e) => {
            previa.Invalidate();
            if (original != null) return;              // editando, não mexe no que o usuário já definiu
            string d = Ymd(data);
            if (d == null) return;
            Escrever(first, cartao ? Calc.Venc1(d, S.cfg.due) : Calc.AddMeses(d, 1));
        };

        Ok.Click += (o, e) => Salvar();

        Preencher(edit);
        MarcarPilulas();
        MarcarPessoas();
        Sincronizar();
    }

    static void Escrever(MaskedTextBox m, string ymd) { m.Text = Calc.Fmt(ymd).Replace("/", ""); }

    /// <summary>Enfileira uma pílula quebrando a linha em M. Devolve o y da linha em que ela coube.</summary>
    static int Fila(List<Botao> lista, Botao b, int M, int y, int alturaLinha) {
        int x = 0;
        if (lista.Count > 0) {
            var u = lista[lista.Count - 1];
            x = u.Left + u.Width + 8;
            y = u.Top;
            if (x + b.Width > M) { x = 0; y += alturaLinha; }
        }
        b.Location = new Point(x, y);
        lista.Add(b);
        return y;
    }

    void MarcarPilulas() {
        foreach (var b in pilulas) { b.Ativo = b.Text == cat; b.Invalidate(); }
    }

    void MarcarPessoas() {
        for (int i = 0; i < pessoas.Count; i++) {
            pessoas[i].Ativo = S.people[i].id == pid;
            pessoas[i].Invalidate();
        }
    }

    void Preencher(Lanc b) {
        if (b == null) {
            if (cartao && S.people.Count > 0) pid = S.people[0].id;
            Escrever(data, Calc.Hoje());
            Escrever(first, cartao ? Calc.Venc1(Calc.Hoje(), S.cfg.due) : Calc.AddMeses(Calc.Hoje(), 1));
            return;
        }
        // pelo id, não pelo nome: duas pessoas podem se chamar igual
        if (cartao) pid = S.people.Any(x => x.id == b.pid) ? b.pid
                        : (S.people.Count > 0 ? S.people[0].id : "");
        else credor.Text = b.pid;
        nome.Text = b.name;
        total.Value = Math.Min(total.Maximum, (decimal)b.total);
        n.Value = Math.Min(n.Maximum, Math.Max(1, b.n));
        Escrever(data, b.date);
        Escrever(first, b.first);
        cat = Calc.CATS.Contains(b.cat) ? b.cat : "Outros";
        rec = b.rec;
        if (!string.IsNullOrEmpty(b.ate)) ate.Text = b.ate.Substring(5, 2) + b.ate.Substring(0, 4);
    }

    /// <summary>Assinatura não tem número de parcelas — o campo vira "cobrar até".</summary>
    void Sincronizar() {
        campoN.Visible = rotN.Visible = !rec;
        campoAte.Visible = rotAte.Visible = rec;
        rotTotal.Desenhar = (g, r) => Ui.Rotulo(g, rec ? "Valor por mês (R$)" : "Valor total (R$)", Ui.LBL, r.X, r.Y + 2);
        rotFirst.Desenhar = (g, r) => Ui.Rotulo(g, rec ? "1ª cobrança" : "1ª parcela vence", Ui.LBL, r.X, r.Y + 2);
        rotTotal.Invalidate(); rotFirst.Invalidate();
        interruptor.Invalidate();
        previa.Invalidate();
    }

    /// <summary>O texto da faixa de conferência: de quanto, quantas, primeira e última.</summary>
    string Conferencia() {
        double t = (double)total.Value;
        string f = Ymd(first);
        if (t <= 0) return "preencha o valor pra ver o cálculo";
        if (f == null) return "confira a data da 1ª " + (rec ? "cobrança" : "parcela");
        if (rec) {
            string fim = Ym(ate);
            return Calc.Brl(t) + " por mês · primeira em " + Calc.Fmt(f) +
                   (string.IsNullOrEmpty(ate.Text.Trim()) || ate.Text.Contains("_")
                        ? ", sem fim"
                        : fim == null ? ", confira o mês final" : ", última em " + Calc.MesLabel(fim));
        }
        int q = (int)n.Value;
        var v = Calc.Dividir(t, q);
        return q + "x de " + Calc.Brl(v[0]) + (v[q - 1] != v[0] ? " (última " + Calc.Brl(v[q - 1]) + ")" : "") +
               " · primeira em " + Calc.Fmt(f) + ", última em " + Calc.Fmt(Calc.AddMeses(f, q - 1));
    }

    void Salvar() {
        string dono = cartao ? pid : credor.Text.Trim();
        if (string.IsNullOrEmpty(dono)) {
            Aplicacao.Erro(cartao ? "Escolha a pessoa." : "Diga para quem você deve."); return;
        }
        if (string.IsNullOrEmpty(nome.Text.Trim())) { Aplicacao.Erro("Falta dizer o que é."); return; }
        if (total.Value <= 0) { Aplicacao.Erro("O valor precisa ser maior que zero."); return; }

        string d = Ymd(data), f = Ymd(first);
        if (d == null) { Aplicacao.Erro("A data não está no formato dd/mm/aaaa."); return; }
        if (f == null) { Aplicacao.Erro("A data da 1ª parcela não está no formato dd/mm/aaaa."); return; }

        string fim = "";
        if (rec && !ate.Text.Contains("_") && ate.Text.Trim().Length > 0) {
            fim = Ym(ate);
            if (fim == null) { Aplicacao.Erro("O mês final não está no formato mm/aaaa."); return; }
        }

        Resultado = new Lanc {
            id = original != null ? original.id : Calc.NovoId(),
            pid = dono, name = nome.Text.Trim(), total = (double)total.Value,
            n = rec ? 1 : (int)n.Value, date = d, first = f, cat = cat,
            rec = rec, ate = fim,
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}

/* ============================== pessoa ============================== */

public class FormPessoa : Dialogo {
    public Pessoa Resultado;
    public bool Excluir;

    string cor;
    readonly List<Card> quadrados = new List<Card>();

    public FormPessoa(Estado s, Pessoa edit)
        : base("pessoa", edit != null ? "Editar pessoa" : "Nova pessoa",
               "Quem usa o cartão ganha uma cor e um total em aberto.", 480, 262, "Salvar") {
        cor = edit != null ? edit.cor : Calc.CORES[s.people.Count % Calc.CORES.Length];
        const int M = 428;

        var nome = new TextBox { BorderStyle = BorderStyle.None };
        var fone = new TextBox { BorderStyle = BorderStyle.None };
        if (edit != null) { nome.Text = edit.nome; fone.Text = edit.fone; }

        Rot("Nome", 0, 0, M);
        Campo(nome, 0, 18, M);

        Rot("WhatsApp (DDD + número)", 0, 78, M);
        Campo(fone, 0, 96, M);

        var aviso = new Card {
            Location = new Point(0, 146), Size = new Size(M, 20), BackColor = Ui.CARD,
            Fundo = Color.Transparent, Borda = Color.Transparent,
        };
        aviso.Desenhar = (g, r) => {
            bool tem = new string((fone.Text ?? "").Where(char.IsDigit).ToArray()).Length >= 10;
            Ui.Icone(g, tem ? "check" : "alerta", new RectangleF(r.X, r.Y + 3, 14, 14), tem ? Ui.OK : Ui.WARN);
            Ui.Txt(g, tem ? "dá pra abrir a cobrança no WhatsApp por esta pessoa"
                          : "sem número a cobrança no WhatsApp não abre",
                   Ui.F(12), tem ? Ui.OK : Ui.WARN, new Rectangle(r.X + 20, r.Y, r.Width - 20, 20));
        };
        fone.TextChanged += (o, e) => aviso.Invalidate();
        Corpo.Controls.Add(aviso);

        Rot("Cor", 0, 180, M);
        int x = 0;
        foreach (var c in Calc.CORES) {
            string dessa = c;
            var q = new Card {
                Location = new Point(x, 200), Size = new Size(38, 38), Raio = 12, BackColor = Ui.CARD,
                Fundo = ColorTranslator.FromHtml(dessa), Borda = Color.Transparent,
            };
            q.Desenhar = (g, r) => {
                if (dessa != cor) return;
                // anel duplo: um vão na cor do cartão e o anel do acento por fora
                Ui.Borda(g, Rectangle.Inflate(r, 3, 3), 15, Ui.CARD, 3);
                Ui.Borda(g, Rectangle.Inflate(r, 5, 5), 17, Ui.ACC, 2);
            };
            q.Clicavel(() => { cor = dessa; foreach (var o in quadrados) o.Invalidate(); });
            q.Fora = new Padding(6);
            q.Size = new Size(50, 50);
            q.Location = new Point(x - 6, 194);
            quadrados.Add(q);
            Corpo.Controls.Add(q);
            x += 50;
        }

        if (edit != null) {
            var excluir = new Botao("Excluir pessoa", "excluir") { BackColor = Ui.CARD, Perigo = true };
            excluir.Location = new Point(26, 22);
            excluir.Click += (o, e) => {
                if (!Aplicacao.Confirma("Excluir " + edit.nome + " e todas as compras dela?")) return;
                Excluir = true;
                DialogResult = DialogResult.OK;
                Close();
            };
            Rodape(excluir);
        }

        Ok.Click += (o, e) => {
            if (string.IsNullOrEmpty(nome.Text.Trim())) { Aplicacao.Erro("Falta o nome."); return; }
            Resultado = new Pessoa {
                id = edit != null ? edit.id : Calc.NovoId(),
                nome = nome.Text.Trim(), fone = fone.Text.Trim(), cor = cor,
            };
            DialogResult = DialogResult.OK;
            Close();
        };
    }
}

}
