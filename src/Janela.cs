// Janela principal: a escolha do módulo e, dentro de cada um, as abas.
// Cartão e dívidas compartilham quase tudo — o que muda é a lista e o dicionário de pagos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using A = CardControl.Aplicacao;

namespace CardControl {

public class Janela : Form {
    Estado S;
    string modo = "hub";          // hub | cartao | dividas
    string aba = "Resumo";
    string filtro = "abertas";
    bool soFuturo;

    static readonly Dictionary<string, string[]> ABAS = new Dictionary<string, string[]> {
        { "cartao",  new[] { "Resumo", "Por mês", "Parcelas", "Compras", "Pessoas" } },
        { "dividas", new[] { "Resumo", "Por mês", "Parcelas", "Dívidas" } },
    };

    readonly Label titulo = new Label();
    readonly FlowLayoutPanel acoes = new FlowLayoutPanel();
    readonly FlowLayoutPanel nav = new FlowLayoutPanel();
    readonly Panel conteudo = new Panel();

    public Janela(Estado s) {
        S = s;
        Text = "CardControl";
        BackColor = A.BG; ForeColor = A.FG;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(1040, 720);
        MinimumSize = new Size(880, 560);
        StartPosition = FormStartPosition.CenterScreen;

        conteudo.Dock = DockStyle.Fill;
        conteudo.AutoScroll = true;
        conteudo.Padding = new Padding(16, 12, 16, 24);
        conteudo.BackColor = A.BG;

        nav.Dock = DockStyle.Top;
        nav.Height = 40;
        nav.Padding = new Padding(14, 6, 0, 0);
        nav.BackColor = A.SURFACE;

        var topo = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = A.SURFACE };
        titulo.Dock = DockStyle.Left;
        titulo.Width = 520;
        titulo.TextAlign = ContentAlignment.MiddleLeft;
        titulo.Padding = new Padding(14, 0, 0, 0);
        titulo.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
        acoes.Dock = DockStyle.Right;
        acoes.Width = 480;
        acoes.FlowDirection = FlowDirection.RightToLeft;
        acoes.Padding = new Padding(0, 11, 14, 0);
        acoes.WrapContents = false;
        topo.Controls.Add(acoes);
        topo.Controls.Add(titulo);

        Controls.Add(conteudo);
        Controls.Add(nav);
        Controls.Add(topo);

        Render();
    }

    /* ---------------------------- estado ---------------------------- */

    bool Cartao { get { return modo == "cartao"; } }
    Dictionary<string, string> Pagos { get { return Cartao ? S.paid : S.dpaid; } }
    List<Parcela> Parcelas() { return Calc.Parcelas(Cartao ? S.buys : S.debts, Pagos); }
    List<TotalPor> Colunas() { return Cartao ? Calc.ColunasCartao(S) : Calc.ColunasDividas(S); }

    void Gravar() {
        try { Cofre.Salvar(S); }
        catch (Exception e) {
            A.Erro("Não deu pra salvar: " + e.Message +
                   "\n\nExporte um backup em ⚙ antes de fechar o programa.");
        }
    }

    void IrPara(string m) { modo = m; aba = "Resumo"; Render(); }

    /* ---------------------------- desenho ---------------------------- */

    void Render() {
        conteudo.SuspendLayout();
        conteudo.Controls.Clear();
        acoes.Controls.Clear();
        nav.Controls.Clear();

        try { Desenhar(); }
        catch (Exception e) {
            // um erro de tela não pode deixar os dados presos lá dentro
            var p = Pilha();
            Add(p, A.Secao("Algo quebrou ao desenhar esta tela."), 30);
            Add(p, A.Txt(e.Message, 9f, A.MUTED), 24);
            Add(p, A.Barra(A.Btn("Voltar ao início", (o, ev) => IrPara("hub")),
                           A.Btn("Configurações e backup", (o, ev) => AbrirCfg())), 40);
            conteudo.Controls.Add(p);
        }

        conteudo.ResumeLayout();
    }

    /// <summary>
    /// Monta todas as telas uma vez, sem a rede de proteção do Render(): é assim que os
    /// testes veem um layout estourar antes do usuário ver "algo quebrou ao desenhar".
    /// </summary>
    public void MontarTodasAsTelas() {
        foreach (var m in new[] { "hub", "cartao", "dividas" }) {
            modo = m;
            foreach (var a in m == "hub" ? new[] { "Resumo" } : ABAS[m]) {
                aba = a;
                foreach (var f in new[] { "abertas", "pagas", "todas" }) {
                    filtro = f;
                    conteudo.Controls.Clear(); acoes.Controls.Clear(); nav.Controls.Clear();
                    Desenhar();
                }
            }
        }
        modo = "hub"; aba = "Resumo"; filtro = "abertas";
    }

    void Desenhar() {
        acoes.Controls.Add(A.Btn("⚙", (o, e) => AbrirCfg()));

        if (modo == "hub") {
            titulo.Text = "CardControl";
            nav.Visible = false;
            conteudo.Controls.Add(VHub());
            return;
        }

        nav.Visible = true;
        var ps = Parcelas();
        var aberto = ps.Where(x => !x.pago).ToList();

        titulo.Text = "CardControl · " + (Cartao ? "cartão" : "dívidas") + " · " +
                      Calc.Brl(aberto.Sum(x => x.v)) + (Cartao ? " a receber" : " a pagar");

        acoes.Controls.Add(Cartao
            ? A.Btn("+ Compra", (o, e) => NovoLanc(null), true)
            : A.Btn("+ Dívida", (o, e) => NovoLanc(null), true));
        acoes.Controls.Add(A.Btn("◀ módulos", (o, e) => IrPara("hub")));

        foreach (var nome in ABAS[modo]) {
            string dessa = nome;
            var b = A.Btn(dessa, (o, e) => { aba = dessa; Render(); }, dessa == aba);
            b.Margin = new Padding(0, 0, 4, 0);
            nav.Controls.Add(b);
        }

        if (Cartao && S.people.Count == 0) {
            var p = Pilha();
            Add(p, A.Secao("Comece cadastrando quem usa o cartão."), 30);
            Add(p, A.Barra(A.Btn("Cadastrar pessoa", (o, e) => NovaPessoa(null), true)), 40);
            conteudo.Controls.Add(p);
            return;
        }
        if (!Cartao && S.debts.Count == 0) {
            var p = Pilha();
            Add(p, A.Secao("Nenhuma dívida cadastrada."), 30);
            Add(p, A.Txt("Cadastre o que você deve e o app calcula parcelas, vencimentos e o que falta quitar.",
                         9f, A.MUTED), 24);
            Add(p, A.Barra(A.Btn("Cadastrar dívida", (o, e) => NovoLanc(null), true)), 40);
            conteudo.Controls.Add(p);
            return;
        }

        var totais = Calc.Totais(ps, Colunas());
        string ym = Calc.MesDe(Calc.Hoje());

        switch (aba) {
            case "Resumo":   conteudo.Controls.Add(Cartao ? VResumoCartao(ps, totais, ym)
                                                          : VResumoDividas(ps, totais, ym)); break;
            case "Por mês":  conteudo.Controls.Add(VPorMes(ps, ym)); break;
            case "Parcelas": conteudo.Controls.Add(VParcelas(ps)); break;
            case "Compras":  conteudo.Controls.Add(VCompras()); break;
            case "Pessoas":  conteudo.Controls.Add(VPessoas(totais)); break;
            case "Dívidas":  conteudo.Controls.Add(VDividas()); break;
        }
    }

    /* -------- empilhar blocos numa coluna que acompanha a largura -------- */

    static TableLayoutPanel Pilha() {
        var t = new TableLayoutPanel {
            Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent,
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    static void Add(TableLayoutPanel p, Control c, int altura) {
        c.Dock = DockStyle.Fill;
        c.Margin = new Padding(0, 0, 0, 10);
        p.RowStyles.Add(altura > 0 ? new RowStyle(SizeType.Absolute, altura + 10)
                                   : new RowStyle(SizeType.AutoSize));
        p.Controls.Add(c, 0, p.RowCount);
        p.RowCount++;
    }

    /* ---------------------------- tela inicial ---------------------------- */

    Control VHub() {
        var receber = Calc.Parcelas(S.buys, S.paid).Where(x => !x.pago).ToList();
        var pagar = Calc.Parcelas(S.debts, S.dpaid).Where(x => !x.pago).ToList();
        int atrasadas = pagar.Count(x => Calc.DiasAte(x.venc) < 0);
        double aReceber = receber.Sum(x => x.v), aPagar = pagar.Sum(x => x.v);

        var p = Pilha();
        Add(p, A.Secao("O que você quer controlar?"), 30);
        Add(p, A.Txt("Os dois módulos ficam neste mesmo programa e no mesmo backup.", 9f, A.MUTED), 22);

        var linha = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        linha.Controls.Add(Modulo("💳  Cartão compartilhado",
            "Quem usou o cartão, quanto cada pessoa deve e quanto falta receber.",
            Calc.Brl(aReceber), aReceber > 0 ? A.WARN : A.OK, "a receber das pessoas",
            S.people.Count + " pessoas · " + S.buys.Count + " compras", () => IrPara("cartao")), 0, 0);
        linha.Controls.Add(Modulo("🏦  Dívidas pessoais",
            "O que você deve, para quem, em quantas parcelas e quando vence.",
            Calc.Brl(aPagar), atrasadas > 0 ? A.BAD : aPagar > 0 ? A.WARN : A.OK,
            atrasadas > 0 ? atrasadas + " parcela(s) atrasada(s)" : "a pagar",
            S.debts.Count + " dívidas · " + pagar.Count + " parcelas em aberto", () => IrPara("dividas")), 1, 0);
        Add(p, linha, 230);
        return p;
    }

    Panel Modulo(string tit, string desc, string valor, Color cor, string sob, string rodape, Action ir) {
        var painel = new Panel { BackColor = A.SURFACE, Padding = new Padding(18), Margin = new Padding(0, 0, 12, 0),
                                 Cursor = Cursors.Hand };
        var pilha = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent };
        pilha.Controls.Add(new Label { Text = tit, Height = 26, Dock = DockStyle.Top, ForeColor = A.FG,
                                       Font = new Font("Segoe UI", 12f, FontStyle.Bold), AutoSize = false });
        pilha.Controls.Add(new Label { Text = desc, Height = 40, Dock = DockStyle.Top, ForeColor = A.MUTED,
                                       AutoSize = false });
        pilha.Controls.Add(new Label { Text = valor, Height = 42, Dock = DockStyle.Top, ForeColor = cor,
                                       Font = new Font("Segoe UI", 19f, FontStyle.Bold), AutoSize = false });
        pilha.Controls.Add(new Label { Text = sob, Height = 20, Dock = DockStyle.Top, ForeColor = A.MUTED,
                                       Font = new Font("Segoe UI", 8f), AutoSize = false });
        pilha.Controls.Add(new Label { Text = rodape, Height = 22, Dock = DockStyle.Top, ForeColor = A.MUTED,
                                       Font = new Font("Segoe UI", 8f), AutoSize = false });

        // botão de verdade: o cartão inteiro também abre no clique, mas só o botão pega o teclado
        var abrir = A.Btn("Abrir  →", (o, e) => ir());
        abrir.Dock = DockStyle.Top;
        pilha.Controls.Add(abrir);

        painel.Controls.Add(pilha);
        painel.Dock = DockStyle.Fill;

        EventHandler clique = (o, e) => ir();
        painel.Click += clique;
        pilha.Click += clique;
        foreach (Control c in pilha.Controls) if (!(c is Button)) c.Click += clique;
        return painel;
    }

    /* ---------------------------- resumos ---------------------------- */

    Control VResumoCartao(List<Parcela> ps, List<TotalPor> t, string ym) {
        var aberto = ps.Where(x => !x.pago).ToList();
        var fatura = ps.Where(x => Calc.MesDe(x.venc) == ym).ToList();
        double fatTotal = fatura.Sum(x => x.v), fatAberto = fatura.Where(x => !x.pago).Sum(x => x.v);
        double gasto = ps.Sum(x => x.v), pago = gasto - aberto.Sum(x => x.v);
        int diaVenc = Math.Min(S.cfg.due, Calc.DiasNoMes(int.Parse(ym.Substring(0, 4)), int.Parse(ym.Substring(5, 2))));
        string venc = ym + "-" + diaVenc.ToString("D2");

        var cards = new List<Panel> {
            A.Stat("Fatura de " + Calc.MesLabel(ym), Calc.Brl(fatTotal), "vence " + Calc.Fmt(venc), A.ACCENT),
            A.Stat("Falta pagar", Calc.Brl(fatAberto),
                   fatura.Count(x => !x.pago) + " parcelas no mês", fatAberto > 0 ? A.WARN : A.OK),
            A.Stat("Total gasto", Calc.Brl(gasto), S.buys.Count + " compras", A.FG),
            A.Stat("Já pago", Calc.Brl(pago), "", A.OK),
        };
        if (S.cfg.limit > 0) {
            double usado = aberto.Sum(x => x.v);
            cards.Add(A.Stat("Limite usado", Calc.Brl(usado), "de " + Calc.Brl(S.cfg.limit),
                             usado / S.cfg.limit >= .8 ? A.BAD : A.ACCENT));
        }

        var p = Pilha();
        Add(p, A.FaixaStats(cards.ToArray()), A.ALTURA_STATS);
        Add(p, A.Secao("Quem deve"), 26);

        var g = A.Grade();
        g.Columns.Add("quem", "pessoa");
        g.Columns.Add("abertas", "parcelas abertas");
        g.Columns.Add("prox", "próxima");
        g.Columns.Add("fat", "fatura " + Calc.MesLabel(ym));
        g.Columns.Add("deve", "total em aberto");
        Alinhar(g, "fat", "deve");
        foreach (var x in t) {
            double naFatura = fatura.Where(y => y.pid == x.id && !y.pago).Sum(y => y.v);
            int i = g.Rows.Add(x.nome, x.abertas + " abertas",
                               x.prox != null ? Calc.Fmt(x.prox.venc) : "—",
                               Calc.Brl(naFatura), Calc.Brl(x.deve));
            g.Rows[i].Tag = x;
            g.Rows[i].Cells["quem"].Style.ForeColor = ColorTranslator.FromHtml(x.cor);
            g.Rows[i].Cells["fat"].Style.ForeColor = naFatura > 0 ? A.ACCENT : A.MUTED;
            g.Rows[i].Cells["deve"].Style.ForeColor = x.deve > 0 ? A.WARN : A.OK;
        }
        int j = g.Rows.Add("total", "", "", Calc.Brl(fatAberto), Calc.Brl(aberto.Sum(x => x.v)));
        g.Rows[j].DefaultCellStyle.BackColor = A.SURF2;
        g.Rows[j].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        Add(p, g, 60 + t.Count * 30);

        Add(p, A.Barra(A.Btn("Cobrar no WhatsApp", (o, e) => Cobrar(g, ym, venc))), 38);

        Add(p, A.Secao("Próximas parcelas"), 26);
        var prox = GradeParcelas(aberto.Take(6).ToList(), true);
        Add(p, prox, 42 + Math.Max(1, Math.Min(6, aberto.Count)) * 30);
        return p;
    }

    Control VResumoDividas(List<Parcela> ps, List<TotalPor> t, string ym) {
        var aberto = ps.Where(x => !x.pago).ToList();
        double total = ps.Sum(x => x.v), pago = total - aberto.Sum(x => x.v);
        var mes = ps.Where(x => Calc.MesDe(x.venc) == ym).ToList();
        double mesAberto = mes.Where(x => !x.pago).Sum(x => x.v);
        int atrasadas = aberto.Count(x => Calc.DiasAte(x.venc) < 0);
        double pct = total > 0 ? pago / total * 100 : 0;

        var p = Pilha();
        Add(p, A.FaixaStats(
            A.Stat("Total devido", Calc.Brl(total), S.debts.Count + " dívidas", A.FG),
            A.Stat("Já paguei", Calc.Brl(pago), ps.Count(x => x.pago) + " parcelas", A.OK),
            A.Stat("Falta pagar", Calc.Brl(aberto.Sum(x => x.v)),
                   aberto.Count + " parcelas em aberto", aberto.Count > 0 ? A.WARN : A.OK),
            A.Stat("Vence em " + Calc.MesLabel(ym), Calc.Brl(mesAberto),
                   atrasadas > 0 ? atrasadas + " atrasadas" : mes.Count + " parcelas no mês",
                   atrasadas > 0 ? A.BAD : A.ACCENT)), A.ALTURA_STATS);

        Add(p, A.Txt("Progresso de quitação: " + Calc.Brl(pago) + " de " + Calc.Brl(total) +
                     " · " + pct.ToString("F0") + "%", 9f, A.MUTED), 22);
        Add(p, A.BarraProgresso(pct, A.OK), 8);

        Add(p, A.Secao("Para quem eu devo"), 26);
        var g = A.Grade();
        g.Columns.Add("quem", "credor");
        g.Columns.Add("abertas", "parcelas abertas");
        g.Columns.Add("prox", "próxima");
        g.Columns.Add("mes", "vence " + Calc.MesLabel(ym));
        g.Columns.Add("deve", "total em aberto");
        Alinhar(g, "mes", "deve");
        foreach (var x in t) {
            double noMes = mes.Where(y => y.pid == x.id && !y.pago).Sum(y => y.v);
            int i = g.Rows.Add(x.nome, x.abertas + " abertas",
                               x.prox != null ? Calc.Fmt(x.prox.venc) : "—",
                               Calc.Brl(noMes), Calc.Brl(x.deve));
            g.Rows[i].Cells["quem"].Style.ForeColor = ColorTranslator.FromHtml(x.cor);
            g.Rows[i].Cells["mes"].Style.ForeColor = noMes > 0 ? A.ACCENT : A.MUTED;
            g.Rows[i].Cells["deve"].Style.ForeColor = x.deve > 0 ? A.WARN : A.OK;
        }
        int j = g.Rows.Add("total", "", "", Calc.Brl(mesAberto), Calc.Brl(aberto.Sum(x => x.v)));
        g.Rows[j].DefaultCellStyle.BackColor = A.SURF2;
        g.Rows[j].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        Add(p, g, 60 + t.Count * 30);

        Add(p, A.Secao("Próximas parcelas"), 26);
        Add(p, GradeParcelas(aberto.Take(6).ToList(), true), 42 + Math.Max(1, Math.Min(6, aberto.Count)) * 30);
        return p;
    }

    void Cobrar(DataGridView g, string ym, string venc) {
        if (g.CurrentRow == null || !(g.CurrentRow.Tag is TotalPor)) {
            A.Erro("Selecione a pessoa na tabela acima."); return;
        }
        var x = (TotalPor)g.CurrentRow.Tag;
        var pessoa = S.people.FirstOrDefault(y => y.id == x.id);
        string fone = pessoa == null ? "" : new string((pessoa.fone ?? "").Where(char.IsDigit).ToArray());
        if (fone.Length < 10) { A.Erro("Essa pessoa não tem WhatsApp cadastrado (aba Pessoas)."); return; }

        var fatura = Calc.Parcelas(S.buys, S.paid)
            .Where(y => y.pid == x.id && !y.pago && Calc.MesDe(y.venc) == ym).Sum(y => y.v);
        string msg = string.Format("Oi {0}! Na fatura de {1} (vence {2}) ficaram {3} pra você. Total em aberto: {4}.",
            x.nome, Calc.MesLabel(ym), Calc.Fmt(venc), Calc.Brl(fatura), Calc.Brl(x.deve));
        try { Process.Start("https://wa.me/55" + fone + "?text=" + Uri.EscapeDataString(msg)); }
        catch (Exception e) { A.Erro("Não deu pra abrir o WhatsApp: " + e.Message); }
    }

    /* ---------------------------- por mês ---------------------------- */

    Control VPorMes(List<Parcela> ps, string ym) {
        var cols = Colunas();
        var meses = ps.Select(x => Calc.MesDe(x.venc)).Distinct()
                      .Where(m => !soFuturo || string.Compare(m, ym, StringComparison.Ordinal) >= 0)
                      .OrderBy(m => m, StringComparer.Ordinal).ToList();

        var p = Pilha();
        var btn = A.Btn((soFuturo ? "✓ " : "") + "esconder meses passados",
                        (o, e) => { soFuturo = !soFuturo; Render(); }, soFuturo);
        Add(p, A.Barra(btn, A.Txt("valores em aberto · ✓ = mês quitado", 9f, A.MUTED)), 38);

        if (meses.Count == 0) { Add(p, A.Txt("Sem parcelas ainda.", 9f, A.MUTED), 24); return p; }

        // fatia[mes][coluna] = { aberto, pago }
        var aberto = new Dictionary<string, Dictionary<string, double>>();
        var quitado = new Dictionary<string, Dictionary<string, double>>();
        foreach (var x in ps) {
            var alvo = x.pago ? quitado : aberto;
            string m = Calc.MesDe(x.venc);
            if (!alvo.ContainsKey(m)) alvo[m] = new Dictionary<string, double>();
            alvo[m][x.pid] = (alvo[m].ContainsKey(x.pid) ? alvo[m][x.pid] : 0) + x.v;
        }
        Func<Dictionary<string, Dictionary<string, double>>, string, string, double> cel =
            (d, m, id) => d.ContainsKey(m) && d[m].ContainsKey(id) ? d[m][id] : 0;

        var g = A.Grade();
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        g.Columns.Add("mes", "mês");
        foreach (var c in cols) g.Columns.Add("c_" + c.id, c.nome);
        g.Columns.Add("tot", "total");

        foreach (var m in meses) {
            var celulas = new List<object> { Calc.MesLabel(m) };
            double somaMes = 0;
            foreach (var c in cols) {
                double ab = cel(aberto, m, c.id), pg = cel(quitado, m, c.id);
                somaMes += ab;
                celulas.Add(ab > 0 ? Calc.Brl(ab) : pg > 0 ? "✓" : "—");
            }
            celulas.Add(somaMes > 0 ? Calc.Brl(somaMes) : "✓");
            int i = g.Rows.Add(celulas.ToArray());

            bool atrasado = string.Compare(m, ym, StringComparison.Ordinal) < 0 && somaMes > 0;
            if (m == ym) {
                g.Rows[i].DefaultCellStyle.BackColor = A.SEL;
                g.Rows[i].Cells["mes"].Value = Calc.MesLabel(m) + "  (atual)";
            } else if (atrasado) {
                g.Rows[i].Cells["mes"].Style.ForeColor = A.BAD;
                g.Rows[i].Cells["mes"].Value = Calc.MesLabel(m) + "  (atrasado)";
            }
            for (int k = 0; k < cols.Count; k++)
                if (cel(aberto, m, cols[k].id) == 0 && cel(quitado, m, cols[k].id) > 0)
                    g.Rows[i].Cells[k + 1].Style.ForeColor = A.OK;
        }

        var rodape = new List<object> { "total em aberto" };
        double geral = 0;
        foreach (var c in cols) {
            double somaCol = meses.Sum(m => cel(aberto, m, c.id));
            geral += somaCol;
            rodape.Add(Calc.Brl(somaCol));
        }
        rodape.Add(Calc.Brl(geral));
        int f = g.Rows.Add(rodape.ToArray());
        g.Rows[f].DefaultCellStyle.BackColor = A.SURF2;
        g.Rows[f].DefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

        Add(p, g, 60 + (meses.Count + 1) * 30);
        Add(p, A.Txt("Cada coluna é " + (Cartao ? "uma pessoa" : "um credor") +
                     "; cada linha, o mês de vencimento das parcelas.", 8.5f, A.MUTED), 22);
        return p;
    }

    /* ---------------------------- parcelas ---------------------------- */

    Control VParcelas(List<Parcela> ps) {
        var rows = ps.Where(x => filtro == "todas" || (filtro == "pagas" ? x.pago : !x.pago)).ToList();

        var p = Pilha();
        var botoes = new List<Control>();
        foreach (var f in new[] { "abertas", "pagas", "todas" }) {
            string dessa = f;
            botoes.Add(A.Btn(dessa, (o, e) => { filtro = dessa; Render(); }, dessa == filtro));
        }
        botoes.Add(A.Txt("clique duplo na linha marca ou desmarca como paga", 9f, A.MUTED));
        Add(p, A.Barra(botoes.ToArray()), 38);

        if (rows.Count == 0) { Add(p, A.Txt("Nenhuma parcela " + filtro + ".", 9f, A.MUTED), 24); return p; }

        foreach (var grupo in rows.GroupBy(x => Calc.MesDe(x.venc)).OrderBy(x => x.Key, StringComparer.Ordinal)) {
            Add(p, A.Secao(Calc.MesLabel(grupo.Key) + "  ·  " + Calc.Brl(grupo.Sum(x => x.v))), 26);
            Add(p, GradeParcelas(grupo.ToList(), true), 42 + grupo.Count() * 30);
        }
        return p;
    }

    DataGridView GradeParcelas(List<Parcela> ps, bool mostrarQuem) {
        var cols = Colunas();
        var g = A.Grade();
        g.Columns.Add("nome", "o que");
        if (mostrarQuem) g.Columns.Add("quem", Cartao ? "pessoa" : "credor");
        g.Columns.Add("parc", "parcela");
        g.Columns.Add("venc", "vence");
        g.Columns.Add("status", "status");
        g.Columns.Add("valor", "valor");
        Alinhar(g, "valor");

        foreach (var x in ps) {
            var dono = cols.FirstOrDefault(c => c.id == x.pid);
            var celulas = new List<object> { x.nome };
            if (mostrarQuem) celulas.Add(dono != null ? dono.nome : "—");
            celulas.Add(x.rec ? "assinatura · " + Calc.MesLabel(Calc.MesDe(x.venc)) : x.i + "/" + x.n);
            celulas.Add(Calc.Fmt(x.venc));
            Color cor;
            celulas.Add(A.Status(x, out cor));
            celulas.Add(Calc.Brl(x.v));

            int i = g.Rows.Add(celulas.ToArray());
            g.Rows[i].Tag = x.chave;
            g.Rows[i].Cells["status"].Style.ForeColor = cor;
            if (x.pago) g.Rows[i].Cells["nome"].Style.ForeColor = A.MUTED;
            if (mostrarQuem && dono != null) g.Rows[i].Cells["quem"].Style.ForeColor = ColorTranslator.FromHtml(dono.cor);
        }

        g.CellDoubleClick += (o, e) => {
            if (e.RowIndex < 0) return;
            Alternar((string)g.Rows[e.RowIndex].Tag);
        };
        return g;
    }

    void Alternar(string chave) {
        var pagos = Pagos;
        if (pagos.ContainsKey(chave)) pagos.Remove(chave);
        else pagos[chave] = Calc.Hoje();
        Gravar();
        Render();
    }

    /* ---------------------------- listas ---------------------------- */

    Control VCompras() {
        var p = Pilha();
        Add(p, A.Barra(A.Btn("+ Nova compra", (o, e) => NovoLanc(null), true),
                       A.Txt("clique duplo na linha para editar", 9f, A.MUTED)), 38);

        var g = A.Grade();
        g.Columns.Add("nome", "o que");
        g.Columns.Add("quem", "pessoa");
        g.Columns.Add("cat", "categoria");
        g.Columns.Add("data", "data");
        g.Columns.Add("parc", "parcelas");
        g.Columns.Add("total", "total");
        Alinhar(g, "total");

        foreach (var b in S.buys.OrderByDescending(x => x.date, StringComparer.Ordinal)) {
            var pes = S.people.FirstOrDefault(x => x.id == b.pid);
            var vals = Calc.Dividir(b.total, Math.Max(1, b.n));
            int i = g.Rows.Add(b.name + (b.rec ? "  (assinatura)" : ""),
                               pes != null ? pes.nome : "—", b.cat, Calc.Fmt(b.date),
                               b.rec ? "por mês" + (string.IsNullOrEmpty(b.ate) ? "" : " até " + Calc.MesLabel(b.ate))
                                     : b.n + "x de " + Calc.Brl(vals[0]),
                               Calc.Brl(b.total));
            g.Rows[i].Tag = b;
            if (pes != null) g.Rows[i].Cells["quem"].Style.ForeColor = ColorTranslator.FromHtml(pes.cor);
        }
        g.CellDoubleClick += (o, e) => { if (e.RowIndex >= 0) NovoLanc((Lanc)g.Rows[e.RowIndex].Tag); };
        Add(p, g, 42 + Math.Max(1, S.buys.Count) * 30);

        Add(p, A.Barra(A.Btn("Editar", (o, e) => SeSelecionado(g, x => NovoLanc((Lanc)x))),
                       A.Btn("Excluir", (o, e) => SeSelecionado(g, x => ExcluirLanc((Lanc)x)))), 38);
        return p;
    }

    Control VDividas() {
        var p = Pilha();
        Add(p, A.Barra(A.Btn("+ Nova dívida", (o, e) => NovoLanc(null), true),
                       A.Txt("clique duplo na linha para editar · marque as parcelas na aba Parcelas", 9f, A.MUTED)), 38);

        var g = A.Grade();
        g.Columns.Add("nome", "o que");
        g.Columns.Add("quem", "credor");
        g.Columns.Add("cat", "categoria");
        g.Columns.Add("parc", "parcelas");
        g.Columns.Add("pagas", "pagas");
        g.Columns.Add("total", "total");
        Alinhar(g, "total");

        foreach (var d in S.debts.OrderByDescending(x => x.first, StringComparer.Ordinal)) {
            var vals = Calc.Dividir(d.total, Math.Max(1, d.n));
            int pagas = d.rec
                ? S.dpaid.Keys.Count(k => k.StartsWith(d.id + ":"))
                : Enumerable.Range(1, vals.Length).Count(i => S.dpaid.ContainsKey(d.id + ":" + i));
            bool quitada = !d.rec && pagas >= vals.Length;

            int r = g.Rows.Add(d.name + (d.rec ? "  (assinatura)" : ""), d.pid, d.cat,
                               d.rec ? "por mês desde " + Calc.MesLabel(Calc.MesDe(d.first)) +
                                       (string.IsNullOrEmpty(d.ate) ? "" : " até " + Calc.MesLabel(d.ate))
                                     : d.n + "x de " + Calc.Brl(vals[0]) + ", 1ª em " + Calc.Fmt(d.first),
                               quitada ? "quitada" : pagas + (d.rec ? " meses" : "/" + d.n),
                               Calc.Brl(d.total));
            g.Rows[r].Tag = d;
            g.Rows[r].Cells["quem"].Style.ForeColor = ColorTranslator.FromHtml(Calc.CorDoNome(d.pid));
            if (quitada) g.Rows[r].Cells["pagas"].Style.ForeColor = A.OK;
        }
        g.CellDoubleClick += (o, e) => { if (e.RowIndex >= 0) NovoLanc((Lanc)g.Rows[e.RowIndex].Tag); };
        Add(p, g, 42 + Math.Max(1, S.debts.Count) * 30);

        Add(p, A.Barra(A.Btn("Editar", (o, e) => SeSelecionado(g, x => NovoLanc((Lanc)x))),
                       A.Btn("Excluir", (o, e) => SeSelecionado(g, x => ExcluirLanc((Lanc)x)))), 38);
        return p;
    }

    Control VPessoas(List<TotalPor> t) {
        var p = Pilha();
        Add(p, A.Barra(A.Btn("+ Nova pessoa", (o, e) => NovaPessoa(null), true),
                       A.Txt("clique duplo na linha para editar", 9f, A.MUTED)), 38);

        var g = A.Grade();
        g.Columns.Add("nome", "nome");
        g.Columns.Add("fone", "WhatsApp");
        g.Columns.Add("compras", "compras");
        g.Columns.Add("deve", "em aberto");
        Alinhar(g, "deve");

        foreach (var pes in S.people) {
            var tot = t.FirstOrDefault(x => x.id == pes.id);
            int i = g.Rows.Add(pes.nome, string.IsNullOrEmpty(pes.fone) ? "—" : pes.fone,
                               S.buys.Count(b => b.pid == pes.id).ToString(),
                               Calc.Brl(tot != null ? tot.deve : 0));
            g.Rows[i].Tag = pes;
            g.Rows[i].Cells["nome"].Style.ForeColor = ColorTranslator.FromHtml(pes.cor);
            g.Rows[i].Cells["deve"].Style.ForeColor = tot != null && tot.deve > 0 ? A.WARN : A.OK;
        }
        g.CellDoubleClick += (o, e) => { if (e.RowIndex >= 0) NovaPessoa((Pessoa)g.Rows[e.RowIndex].Tag); };
        Add(p, g, 42 + Math.Max(1, S.people.Count) * 30);

        Add(p, A.Barra(A.Btn("Editar", (o, e) => SeSelecionado(g, x => NovaPessoa((Pessoa)x))),
                       A.Btn("Excluir", (o, e) => SeSelecionado(g, x => ExcluirPessoa((Pessoa)x)))), 38);
        return p;
    }

    static void SeSelecionado(DataGridView g, Action<object> acao) {
        if (g.CurrentRow == null || g.CurrentRow.Tag == null) { A.Erro("Selecione uma linha primeiro."); return; }
        acao(g.CurrentRow.Tag);
    }

    static void Alinhar(DataGridView g, params string[] colunas) {
        foreach (var c in colunas)
            g.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    /* ---------------------------- ações ---------------------------- */

    void NovoLanc(Lanc edit) {
        if (Cartao && S.people.Count == 0) { NovaPessoa(null); return; }
        using (var f = new FormLanc(S, Cartao, edit)) {
            if (f.ShowDialog(this) != DialogResult.OK) return;
            var lista = Cartao ? S.buys : S.debts;
            if (edit != null) lista[lista.IndexOf(edit)] = f.Resultado;
            else lista.Add(f.Resultado);
            Gravar(); Render();
        }
    }

    void NovaPessoa(Pessoa edit) {
        using (var f = new FormPessoa(S, edit)) {
            if (f.ShowDialog(this) != DialogResult.OK) return;
            if (edit != null) S.people[S.people.IndexOf(edit)] = f.Resultado;
            else S.people.Add(f.Resultado);
            Gravar();
            if (modo == "hub") IrPara("cartao"); else Render();
        }
    }

    void ExcluirLanc(Lanc x) {
        if (!A.Confirma("Excluir \"" + x.name + "\" e suas parcelas?")) return;
        (Cartao ? S.buys : S.debts).Remove(x);
        LimparPagos(Pagos, x.id);
        Gravar(); Render();
    }

    void ExcluirPessoa(Pessoa p) {
        var compras = S.buys.Where(b => b.pid == p.id).ToList();
        if (!A.Confirma("Excluir " + p.nome + (compras.Count > 0 ? " e suas " + compras.Count + " compras" : "") + "?"))
            return;
        foreach (var b in compras) { LimparPagos(S.paid, b.id); S.buys.Remove(b); }
        S.people.Remove(p);
        Gravar(); Render();
    }

    /// <summary>Apaga as marcações de parcela paga de um lançamento que deixou de existir.</summary>
    static void LimparPagos(Dictionary<string, string> pagos, string id) {
        foreach (var k in pagos.Keys.Where(k => k.StartsWith(id + ":")).ToList()) pagos.Remove(k);
    }

    void AbrirCfg() {
        using (var f = new FormCfg(S)) {
            f.ShowDialog(this);
            if (!f.Mudou) return;
            bool trocou = !ReferenceEquals(f.Estado, S);
            S = f.Estado;
            Gravar();
            if (trocou) IrPara("hub"); else Render();
        }
    }
}

}
