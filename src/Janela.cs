// Janela principal do redesenho v5: menu lateral fixo, cabeçalho de tela e o conteúdo.
// Cartão e dívidas continuam compartilhando quase tudo — o que muda é a lista de
// lançamentos e o dicionário de parcelas pagas.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MoneyControl {

public class Janela : Form {
    Estado S;
    string modo = "cartao";      // módulo cujo bloco de itens o menu mostra
    string aba = "";             // "" = Início · "config" = Configurações · senão a aba do módulo
    string filtro = "abertas";   // abertas | atrasadas | pagas | todas
    bool soFuturo;

    static readonly Dictionary<string, string[]> ABAS = new Dictionary<string, string[]> {
        { "cartao",  new[] { "Resumo", "Por mês", "Parcelas", "Compras", "Pessoas" } },
        { "dividas", new[] { "Resumo", "Por mês", "Parcelas", "Dívidas" } },
    };
    static readonly Dictionary<string, string> ICONE = new Dictionary<string, string> {
        { "Resumo", "resumo" }, { "Por mês", "calendario" }, { "Parcelas", "parcelas" },
        { "Compras", "compras" }, { "Pessoas", "pessoas" }, { "Dívidas", "carteira" },
    };

    readonly Panel menu = new Panel();
    readonly Panel itens = new Panel();
    readonly Panel rodapeMenu = new Panel();
    readonly Card marca = new Card();
    readonly Card cabecalho = new Card();
    readonly FlowLayoutPanel acoes = new FlowLayoutPanel();
    readonly Panel conteudo = new Panel();

    public Janela(Estado s) {
        S = s;
        Text = "MoneyControl";
        BackColor = Ui.BG; ForeColor = Ui.FG;
        Font = Ui.F(14);
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(1320, 840);
        MinimumSize = new Size(1180, 800);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        try { Icon = Icon.ExtractAssociatedIcon(Aplicacao.Exe); } catch { }

        conteudo.Dock = DockStyle.Fill;
        conteudo.AutoScroll = true;
        conteudo.Padding = new Padding(28, 22, 28, 36);
        conteudo.BackColor = Ui.BG;

        cabecalho.Dock = DockStyle.Top;
        cabecalho.Height = 88;
        cabecalho.BackColor = Ui.BG;
        cabecalho.Fundo = Color.Transparent;
        cabecalho.Borda = Color.Transparent;
        cabecalho.Padding = new Padding(28, 0, 28, 0);

        acoes.Dock = DockStyle.Right;
        acoes.Width = 560;
        acoes.FlowDirection = FlowDirection.RightToLeft;
        acoes.WrapContents = false;
        acoes.Padding = new Padding(0, 24, 0, 0);
        acoes.BackColor = Ui.BG;
        cabecalho.Controls.Add(acoes);

        var corpo = new Panel { Dock = DockStyle.Fill, BackColor = Ui.BG };
        corpo.Controls.Add(conteudo);
        corpo.Controls.Add(cabecalho);

        menu.Dock = DockStyle.Left;
        menu.Width = Ui.MENU_W;
        menu.BackColor = Ui.NAV;
        itens.Dock = DockStyle.Fill; itens.BackColor = Ui.NAV;
        rodapeMenu.Dock = DockStyle.Bottom; rodapeMenu.Height = 132; rodapeMenu.BackColor = Ui.NAV;
        marca.Dock = DockStyle.Top; marca.Height = 84; marca.BackColor = Ui.NAV;
        marca.Fundo = Color.Transparent; marca.Borda = Color.Transparent;
        marca.Desenhar = (g, r) => {
            Ui.Marca(g, new RectangleF(r.X + 24, r.Y + 23, 40, 40));
            Ui.Txt(g, "MoneyControl", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 74, r.Y + 26, 160, 20));
            Ui.Txt(g, "v" + Aplicacao.VERSAO, Ui.F(12), Ui.LBL, new Rectangle(r.X + 74, r.Y + 45, 160, 16));
        };
        menu.Controls.Add(itens);
        menu.Controls.Add(rodapeMenu);
        menu.Controls.Add(marca);

        Controls.Add(corpo);
        Controls.Add(menu);

        Render();
    }

    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        Ui.BarraEscura(this);
    }

    /* ---------------------------- estado ---------------------------- */

    bool Cartao { get { return modo == "cartao"; } }
    Dictionary<string, string> Pagos { get { return Cartao ? S.paid : S.dpaid; } }
    List<Parcela> Parcelas() { return Calc.Parcelas(Cartao ? S.buys : S.debts, Pagos); }
    List<TotalPor> Colunas() { return Cartao ? Calc.ColunasCartao(S) : Calc.ColunasDividas(S); }
    static string Mes { get { return Calc.MesDe(Calc.Hoje()); } }

    void Gravar() {
        try { Cofre.Salvar(S); }
        catch (Exception e) {
            Aplicacao.Erro("Não deu pra salvar: " + e.Message +
                           "\n\nGere um backup em Configurações antes de fechar o programa.");
        }
    }

    void Ir(string m, string a) { modo = m; aba = a; Render(); }

    /// <summary>Abre uma tela de fora — é o que o menu faz, e o que os testes usam.</summary>
    public void AbrirTela(string m, string a) { Ir(m, a); }

    /// <summary>Dia do vencimento da fatura no mês corrente.</summary>
    string VencFatura(string ym) {
        int dia = Math.Min(S.cfg.due, Calc.DiasNoMes(int.Parse(ym.Substring(0, 4)), int.Parse(ym.Substring(5, 2))));
        return ym + "-" + dia.ToString("D2");
    }

    static string Status(Parcela x, out Color fg, out Color bg) {
        if (x.pago) { fg = Ui.OK; bg = Ui.OKBG; return "paga"; }
        int d = Calc.DiasAte(x.venc);
        if (d < 0) { fg = Ui.BAD; bg = Ui.BADBG; return (-d) + (d == -1 ? " dia atrasada" : " dias atrasada"); }
        if (d == 0) { fg = Ui.WARN; bg = Ui.WARNBG; return "vence hoje"; }
        if (d == 1) { fg = Ui.WARN; bg = Ui.WARNBG; return "vence amanhã"; }
        fg = Ui.FG3; bg = Ui.NEUBG; return "em dia";
    }

    Color CorDe(string pid) {
        var p = S.people.FirstOrDefault(x => x.id == pid);
        if (p != null) return ColorTranslator.FromHtml(p.cor);
        return ColorTranslator.FromHtml(Calc.CorDoNome(pid));
    }

    string NomeDe(string pid) {
        var p = S.people.FirstOrDefault(x => x.id == pid);
        return p != null ? p.nome : pid;
    }

    /* ---------------------------- desenho ---------------------------- */

    void Render() {
        SuspendLayout();
        conteudo.SuspendLayout();
        conteudo.Controls.Clear();
        acoes.Controls.Clear();
        LimparMenu();

        try { Desenhar(); }
        catch (Exception e) {
            // um erro de tela não pode deixar os dados presos lá dentro
            conteudo.Controls.Clear();
            Cabecalho("alerta", "Algo quebrou ao desenhar esta tela", e.Message,
                      Bot("Voltar ao início", "inicio", true, () => Ir(modo, "")),
                      Bot("Configurações", "config", false, () => Ir(modo, "config")));
        }

        conteudo.ResumeLayout();
        ResumeLayout();
    }

    /// <summary>
    /// Monta todas as telas uma vez, sem a rede de proteção do Render(): é assim que os
    /// testes veem um layout estourar antes do usuário ver "algo quebrou ao desenhar".
    /// </summary>
    public void MontarTodasAsTelas() {
        string m0 = modo, a0 = aba, f0 = filtro;
        foreach (var m in new[] { "cartao", "dividas" }) {
            modo = m;
            foreach (var a in new List<string> { "", "config" }.Concat(ABAS[m])) {
                aba = a;
                foreach (var f in new[] { "abertas", "atrasadas", "pagas", "todas" }) {
                    filtro = f;
                    conteudo.Controls.Clear(); acoes.Controls.Clear(); LimparMenu();
                    Desenhar();
                }
            }
        }
        modo = m0; aba = a0; filtro = f0;
    }

    void LimparMenu() {
        foreach (Control c in itens.Controls.Cast<Control>().ToList()) c.Dispose();
        foreach (Control c in rodapeMenu.Controls.Cast<Control>().ToList()) c.Dispose();
        itens.Controls.Clear();
        rodapeMenu.Controls.Clear();
    }

    void Desenhar() {
        MontarMenu();

        if (aba == "") { VInicio(); return; }
        if (aba == "config") { VConfig(); return; }

        var ps = Parcelas();
        var abertas = ps.Where(x => !x.pago).ToList();
        var totais = Calc.Totais(ps, Colunas());
        string ym = Mes;

        if (Cartao && S.people.Count == 0) {
            Cabecalho("pessoas", "Cartão compartilhado", "Ninguém cadastrado ainda.",
                      Bot("Cadastrar pessoa", "mais", true, () => NovaPessoa(null)));
            Pilha(Vazio("pessoas", "Comece cadastrando quem usa o cartão",
                        "Cada pessoa ganha uma cor, um total em aberto e um botão de cobrança no WhatsApp. " +
                        "Sem ninguém cadastrado não dá pra lançar compra.",
                        Bot("Cadastrar pessoa", "mais", true, () => NovaPessoa(null))));
            return;
        }
        if (!Cartao && S.debts.Count == 0) {
            Cabecalho("carteira", "Dívidas pessoais", "Nenhuma dívida cadastrada.",
                      Bot("Cadastrar dívida", "mais", true, () => NovoLanc(null)));
            Pilha(Vazio("carteira", "Nenhuma dívida cadastrada",
                        "Cadastre o que você deve e o app calcula as parcelas, os vencimentos, quanto já foi " +
                        "pago e quando a última parcela cai.",
                        Bot("Cadastrar dívida", "mais", true, () => NovoLanc(null))));
            return;
        }

        switch (aba) {
            case "Resumo":   if (Cartao) VResumoCartao(ps, totais, ym); else VResumoDividas(ps, totais, ym); break;
            case "Por mês":  VPorMes(ps, ym); break;
            case "Parcelas": VParcelas(ps); break;
            case "Compras":  VCompras(); break;
            case "Pessoas":  VPessoas(totais, ym); break;
            case "Dívidas":  VDividas(); break;
        }
    }

    /* ---------------------------- menu lateral ---------------------------- */

    void MontarMenu() {
        var receber = Calc.Parcelas(S.buys, S.paid).Where(x => !x.pago).ToList();
        var pagar = Calc.Parcelas(S.debts, S.dpaid).Where(x => !x.pago).ToList();
        int atrasadasP = pagar.Count(x => Calc.DiasAte(x.venc) < 0);
        int atrasadasR = receber.Count(x => Calc.DiasAte(x.venc) < 0);

        var lista = new List<Control>();
        lista.Add(Ui.RotuloMenu("Geral"));
        lista.Add(Ui.ItemMenu("inicio", "Início", null, Ui.FG3, null, Ui.FG3, aba == "", () => Ir(modo, "")));

        lista.Add(Ui.RotuloMenu("Módulos"));
        lista.Add(Ui.ItemMenu("cartao", "Cartão compartilhado",
                              Calc.Brl(receber.Sum(x => x.v)) + " a receber",
                              receber.Count == 0 ? Ui.OK : atrasadasR > 0 ? Ui.BAD : Ui.ACC,
                              null, Ui.FG3, Cartao && aba != "" && aba != "config",
                              () => Ir("cartao", aba == "" || aba == "config" ? "Resumo" : aba)));
        lista.Add(Ui.ItemMenu("carteira", "Dívidas pessoais",
                              Calc.Brl(pagar.Sum(x => x.v)) + " a pagar",
                              pagar.Count == 0 ? Ui.OK : atrasadasP > 0 ? Ui.BAD : Ui.WARN,
                              null, Ui.FG3, !Cartao && aba != "" && aba != "config",
                              () => Ir("dividas", aba == "" || aba == "config" ? "Resumo" : aba)));

        lista.Add(Ui.RotuloMenu(Cartao ? "Cartão compartilhado" : "Dívidas pessoais"));
        var ps = Cartao ? receber : pagar;
        int atrasadas = Cartao ? atrasadasR : atrasadasP;
        foreach (var nome in ABAS[modo]) {
            string dessa = nome, badge = null;
            Color badgeCor = Ui.FG3;
            if (dessa == "Parcelas") {
                if (atrasadas > 0) { badge = atrasadas + " atrasada" + (atrasadas > 1 ? "s" : ""); badgeCor = Ui.BAD; }
                else badge = ps.Count.ToString();
            } else if (dessa == "Compras") badge = S.buys.Count.ToString();
            else if (dessa == "Dívidas") badge = S.debts.Count.ToString();
            else if (dessa == "Pessoas") badge = S.people.Count.ToString();
            lista.Add(Ui.ItemMenu(ICONE[dessa], dessa, null, Ui.FG3, badge, badgeCor,
                                  aba == dessa, () => Ir(modo, dessa)));
        }
        Empilhar(itens, lista);

        Empilhar(rodapeMenu, new List<Control> {
            Ui.RotuloMenu("Dados"),
            Ui.ItemMenu("backup", "Backup", null, Ui.FG3, null, Ui.FG3, false, () => {
                Aplicacao.ExportarBackup(S); Render();
            }),
            Ui.ItemMenu("config", "Configurações", null, Ui.FG3, null, Ui.FG3, aba == "config",
                        () => Ir(modo, "config")),
        });
    }

    /// <summary>Dock=Top empilha do último para o primeiro: entra invertido pra sair na ordem lida.</summary>
    static void Empilhar(Panel p, List<Control> cs) {
        for (int i = cs.Count - 1; i >= 0; i--) { cs[i].Dock = DockStyle.Top; p.Controls.Add(cs[i]); }
    }

    /* ---------------------------- cabeçalho e blocos ---------------------------- */

    void Cabecalho(string icone, string titulo, string sub, params Control[] bs) {
        cabecalho.Desenhar = (g, r) => {
            var chip = new Rectangle(r.X + 28, r.Y + 22, 44, 44);
            Ui.Chip(g, chip, icone, Ui.ACC, Ui.CHIP, 13);
            int w = r.Width - 28 - chip.Width - 14 - acoes.Width - 24;
            Ui.Txt(g, titulo, Ui.F(20, true), Ui.FG, new Rectangle(chip.Right + 14, r.Y + 22, w, 24));
            Ui.Txt(g, sub, Ui.F(13), Ui.FG3, new Rectangle(chip.Right + 14, r.Y + 46, w, 20));
            Ui.Divisoria(g, r.X, r.Bottom - 1, r.Width);
        };
        for (int i = bs.Length - 1; i >= 0; i--) {
            bs[i].Margin = new Padding(10, 0, 0, 0);
            acoes.Controls.Add(bs[i]);
        }
        // a faixa de ações cobre o que estiver embaixo: ela ocupa só o que os botões pedem
        acoes.Width = bs.Sum(b => b.Width + 10);
        cabecalho.Invalidate();
    }

    Botao Bot(string txt, string icone, bool primario, Action a) {
        var b = new Botao(txt, icone, primario) { BackColor = Ui.BG };
        b.Click += (o, e) => a();
        return b;
    }

    /// <summary>Coluna que empilha os blocos e acompanha a largura do conteúdo.</summary>
    static TableLayoutPanel NovaPilha() {
        var t = new TableLayoutPanel {
            Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent,
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    static void Add(TableLayoutPanel p, Control c, int altura) {
        c.Dock = DockStyle.Fill;
        c.Margin = new Padding(0, 0, 0, 16);
        p.RowStyles.Add(altura > 0 ? new RowStyle(SizeType.Absolute, altura + 16)
                                   : new RowStyle(SizeType.AutoSize));
        p.Controls.Add(c, 0, p.RowCount);
        p.RowCount++;
    }

    /// <summary>Monta a pilha de blocos da tela atual.</summary>
    void Pilha(params Control[] blocos) {
        var p = NovaPilha();
        foreach (var b in blocos) Add(p, b, b.Height);
        conteudo.Controls.Add(p);
    }

    static Card Bloco(int altura, int raio, Action<Graphics, Rectangle> d) {
        return new Card { Height = altura, Raio = raio, Desenhar = d, BackColor = Ui.BG };
    }

    /// <summary>Título de bloco, com um texto secundário à direita.</summary>
    static Card Secao(string s, string dir) {
        var c = new Card { Height = 30, BackColor = Ui.BG, Fundo = Color.Transparent, Borda = Color.Transparent };
        c.Desenhar = (g, r) => {
            Ui.Txt(g, s, Ui.F(17, true), Ui.FG, new Rectangle(r.X, r.Y, r.Width - 240, r.Height));
            if (dir != null) Ui.TxtDir(g, dir, Ui.F(13), Ui.FG3, new Rectangle(r.X, r.Y, r.Width, r.Height));
        };
        return c;
    }

    static TableLayoutPanel Colunas(int altura, float pct, Control a, Control b) {
        var t = new TableLayoutPanel {
            Height = altura, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent,
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, pct));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 - pct));
        // pilha entra ancorada no topo: com Fill a última linha absorveria a sobra da coluna
        a.Dock = a is TableLayoutPanel ? DockStyle.Top : DockStyle.Fill;
        b.Dock = b is TableLayoutPanel ? DockStyle.Top : DockStyle.Fill;
        a.Margin = new Padding(0, 0, 8, 0);
        b.Margin = new Padding(8, 0, 0, 0);
        t.Controls.Add(a, 0, 0);
        t.Controls.Add(b, 1, 0);
        return t;
    }

    static TableLayoutPanel Grade(int altura, int colunas, List<Control> cs) {
        int linhas = (int)Math.Ceiling(cs.Count / (double)colunas);
        var t = new TableLayoutPanel {
            Height = altura * linhas + 16 * (linhas - 1), ColumnCount = colunas, RowCount = linhas,
            BackColor = Color.Transparent,
        };
        for (int i = 0; i < colunas; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colunas));
        for (int i = 0; i < linhas; i++) t.RowStyles.Add(new RowStyle(SizeType.Absolute, altura + 16));
        for (int i = 0; i < cs.Count; i++) {
            cs[i].Dock = DockStyle.Fill;
            cs[i].Margin = new Padding(i % colunas == 0 ? 0 : 8, 0, (i + 1) % colunas == 0 ? 0 : 8, 16);
            t.Controls.Add(cs[i], i % colunas, i / colunas);
        }
        return t;
    }

    /// <summary>Linha de lista: nunca uma tabela vazia, nunca menos de 44px de altura clicável.</summary>
    static Card Linha(int altura, Action<Graphics, Rectangle> d) {
        return new Card {
            Height = altura, Raio = 14, BackColor = Ui.CARD,
            Fundo = Color.Transparent, Borda = Color.Transparent, Desenhar = d,
        };
    }

    /// <summary>Cartão que contém linhas: reserva o cabeçalho no Padding e empilha as linhas.</summary>
    static Card Lista(int cabecalhoH, List<Control> linhas, Action<Graphics, Rectangle> d, int alturaLinha) {
        var c = new Card {
            Raio = 20, BackColor = Ui.BG, Desenhar = d,
            Padding = new Padding(12, cabecalhoH, 12, 12),
            Height = cabecalhoH + Math.Max(1, linhas.Count) * alturaLinha + 12,
        };
        Empilhar(c, linhas);
        foreach (Control x in c.Controls) x.Height = alturaLinha;
        return c;
    }

    static Card Vazio(string icone, string titulo, string texto, Control botao) {
        var c = new Card { Height = 220, Raio = 22, BackColor = Ui.BG };
        c.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + (r.Width - 52) / 2, r.Y + 34, 52, 52), icone, Ui.ACC, Ui.CHIP, 16);
            Ui.TxtCentro(g, titulo, Ui.F(17, true), Ui.FG, new Rectangle(r.X, r.Y + 96, r.Width, 24));
            TextRenderer.DrawText(g, texto, Ui.F(13), new Rectangle(r.X + r.Width / 2 - 260, r.Y + 122, 520, 44),
                Ui.FG3, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
                        TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
        };
        if (botao != null) {
            c.Controls.Add(botao);
            botao.BackColor = Ui.CARD;
            Action pos = () => botao.Location = new Point((c.Width - botao.Width) / 2, 168);
            c.Resize += (o, e) => pos();
            pos();
        }
        return c;
    }

    /// <summary>Ancora um controle à direita de um cartão de largura ainda desconhecida.</summary>
    static void Direita(Card dono, Control c, int margem, int y) {
        dono.Controls.Add(c);
        c.BackColor = Ui.CARD;
        Action pos = () => c.Location = new Point(dono.Width - margem - c.Width, y);
        dono.Resize += (o, e) => pos();
        pos();
        dono.Adotar(c);
    }

    /* ---------------------------- Início ---------------------------- */

    void VInicio() {
        var receber = Calc.Parcelas(S.buys, S.paid);
        var pagar = Calc.Parcelas(S.debts, S.dpaid);
        var recAb = receber.Where(x => !x.pago).ToList();
        var pagAb = pagar.Where(x => !x.pago).ToList();
        string ym = Mes;
        bool faturaAberta = recAb.Any(x => Calc.MesDe(x.venc) == ym);
        bool devendoMes = pagAb.Any(x => Calc.MesDe(x.venc) == ym);

        Cabecalho("inicio", "Início",
                  Calc.Brl(recAb.Sum(x => x.v)) + " a receber no cartão · " +
                  Calc.Brl(pagAb.Sum(x => x.v)) + " a pagar em dívidas",
                  Bot("Nova compra", "mais", true, () => { modo = "cartao"; NovoLanc(null); }),
                  Bot("Nova dívida", "mais", false, () => { modo = "dividas"; NovoLanc(null); }));

        var m1 = ModuloCard("cartao", "Cartão compartilhado",
                            S.people.Count + " pessoas · " + S.buys.Count + " compras",
                            recAb.Sum(x => x.v), recAb.Count == 0 ? Ui.OK : Ui.ACC,
                            recAb.Count == 0 ? "nada em aberto"
                                : recAb.Count + " parcelas em aberto · próxima " + Calc.Fmt(recAb.Min(x => x.venc)),
                            faturaAberta, () => Ir("cartao", "Resumo"));
        var m2 = ModuloCard("carteira", "Dívidas pessoais",
                            S.debts.Count + " dívidas · " + Calc.ColunasDividas(S).Count + " credores",
                            pagAb.Sum(x => x.v),
                            pagAb.Any(x => Calc.DiasAte(x.venc) < 0) ? Ui.BAD : pagAb.Count == 0 ? Ui.OK : Ui.WARN,
                            pagAb.Count == 0 ? "tudo quitado"
                                : pagAb.Count + " parcelas em aberto · próxima " + Calc.Fmt(pagAb.Min(x => x.venc)),
                            devendoMes, () => Ir("dividas", "Resumo"));

        var alertas = Alertas(recAb, pagAb, ym);
        var linhas = alertas.Select(a => (Control)LinhaAlerta(a)).ToList();
        Control bloco;
        if (linhas.Count == 0) {
            bloco = Vazio("check", "Nada pedindo atenção agora",
                          "Nenhuma parcela vencida, nenhuma fatura fechando e ninguém sem WhatsApp cadastrado.", null);
        } else {
            bloco = Lista(56, linhas, (g, r) => {
                Ui.Txt(g, "Precisa de você", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 24, r.Y + 18, 300, 24));
                Ui.TxtDir(g, linhas.Count + (linhas.Count > 1 ? " itens" : " item") + " por urgência", Ui.F(13),
                          Ui.FG3, new Rectangle(r.X, r.Y + 18, r.Width - 24, 24));
            }, 64);
        }

        Pilha(Colunas(214, 50, m1, m2), bloco, FaixaBackup());
    }

    Card ModuloCard(string icone, string titulo, string contagem, double valor, Color cor,
                    string rodape, bool destaque, Action ir) {
        var c = new Card { Height = 214, Raio = 22, BackColor = Ui.BG };
        if (destaque) { c.Fundo = Ui.VERDE_FUNDO; c.Grad = Ui.CARD; c.Borda = Ui.VERDE_BORDA; }
        c.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 26, r.Y + 26, 46, 46), icone, Ui.ACC, Ui.CHIP, 14);
            Ui.Txt(g, titulo, Ui.F(17, true), Ui.FG, new Rectangle(r.X + 84, r.Y + 28, r.Width - 110, 22));
            Ui.Txt(g, contagem, Ui.F(13), Ui.FG3, new Rectangle(r.X + 84, r.Y + 50, r.Width - 110, 20));
            Ui.TxtAjuste(g, Calc.Brl(valor), 44, cor, new Rectangle(r.X + 26, r.Y + 84, r.Width - 52, 48));
            Ui.Txt(g, rodape, Ui.F(12), Ui.FG3, new Rectangle(r.X + 26, r.Y + 132, r.Width - 52, 18));
        };
        c.Clicavel(ir);
        var abrir = Bot("Abrir", "seta", false, ir);
        abrir.BackColor = destaque ? Ui.VERDE_FUNDO : Ui.CARD;
        abrir.Location = new Point(26, 158);
        c.Controls.Add(abrir);
        c.Adotar(abrir);
        return c;
    }

    class Alerta {
        public string icone, titulo, detalhe, botao;
        public Color cor;
        public double valor;
        public Action acao;
    }

    List<Alerta> Alertas(List<Parcela> recAb, List<Parcela> pagAb, string ym) {
        var outp = new List<Alerta>();

        var vencR = recAb.Where(x => !x.rec && Calc.DiasAte(x.venc) < 0).ToList();
        if (vencR.Count > 0)
            outp.Add(new Alerta {
                icone = "alerta", cor = Ui.BAD, valor = vencR.Sum(x => x.v),
                titulo = vencR.Count + " parcela" + (vencR.Count > 1 ? "s vencidas" : " vencida") + " no cartão",
                detalhe = "a mais antiga venceu em " + Calc.Fmt(vencR.Min(x => x.venc)),
                botao = "Ver parcelas",
                acao = () => { filtro = "atrasadas"; Ir("cartao", "Parcelas"); },
            });

        var vencD = pagAb.Where(x => !x.rec && Calc.DiasAte(x.venc) < 0).ToList();
        if (vencD.Count > 0)
            outp.Add(new Alerta {
                icone = "alerta", cor = Ui.BAD, valor = vencD.Sum(x => x.v),
                titulo = vencD.Count + " parcela" + (vencD.Count > 1 ? "s vencidas" : " vencida") + " que você deve",
                detalhe = "a mais antiga venceu em " + Calc.Fmt(vencD.Min(x => x.venc)),
                botao = "Ver parcelas",
                acao = () => { filtro = "atrasadas"; Ir("dividas", "Parcelas"); },
            });

        string venc = VencFatura(ym);
        int dias = Calc.DiasAte(venc);
        double fatura = recAb.Where(x => Calc.MesDe(x.venc) == ym).Sum(x => x.v);
        if (fatura > 0 && dias >= 0 && dias <= 5)
            outp.Add(new Alerta {
                icone = "relogio", cor = Ui.WARN, valor = fatura,
                titulo = dias == 0 ? "A fatura fecha hoje" : dias == 1 ? "A fatura fecha amanhã"
                                                                      : "A fatura fecha em " + dias + " dias",
                detalhe = "vence " + Calc.Fmt(venc) + " · ainda em aberto",
                botao = "Ver parcelas",
                acao = () => { filtro = "abertas"; Ir("cartao", "Parcelas"); },
            });

        // uma linha por assinatura, não uma por mês: quatro linhas iguais de Netflix não ajudam
        foreach (var grupo in recAb.Concat(pagAb).Where(x => x.rec && Calc.DiasAte(x.venc) < 0)
                                                 .GroupBy(x => x.lancId)) {
            var meses = grupo.OrderBy(x => x.venc, StringComparer.Ordinal).ToList();
            var primeira = meses[0];
            bool doCartao = S.buys.Any(b => b.id == primeira.lancId);
            outp.Add(new Alerta {
                icone = "repete", cor = Ui.WARN, valor = meses.Sum(x => x.v),
                titulo = "Assinatura atrasada: " + primeira.nome,
                detalhe = meses.Count == 1
                    ? "a cobrança de " + Calc.MesLabel(Calc.MesDe(primeira.venc)) + " não foi marcada como paga"
                    : meses.Count + " cobranças sem marcar, desde " + Calc.MesLabel(Calc.MesDe(primeira.venc)),
                botao = "Ver parcelas",
                acao = () => { filtro = "atrasadas"; Ir(doCartao ? "cartao" : "dividas", "Parcelas"); },
            });
        }

        var totais = Calc.Totais(Calc.Parcelas(S.buys, S.paid), Calc.ColunasCartao(S));
        foreach (var t in totais.Where(x => x.deve > 0)) {
            var pes = S.people.FirstOrDefault(x => x.id == t.id);
            if (pes == null || new string((pes.fone ?? "").Where(char.IsDigit).ToArray()).Length >= 10) continue;
            var dessa = pes;
            outp.Add(new Alerta {
                icone = "pessoa", cor = Ui.FG3, valor = t.deve,
                titulo = dessa.nome + " está sem WhatsApp cadastrado",
                detalhe = "sem número a cobrança não abre",
                botao = "Cadastrar",
                acao = () => { modo = "cartao"; NovaPessoa(dessa); },
            });
        }
        return outp;
    }

    Card LinhaAlerta(Alerta a) {
        var l = Linha(64, null);
        var btn = Bot(a.botao, null, false, a.acao);
        btn.Height = 34; btn.Font = Ui.F(13, true); btn.Medir();
        l.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 12, r.Y + 14, 36, 36), a.icone, a.cor, Ui.Alfa(a.cor, .12), 12);
            int larg = r.Width - 60 - btn.Width - 150;
            Ui.Txt(g, a.titulo, Ui.F(14, true), Ui.FG, new Rectangle(r.X + 60, r.Y + 13, larg, 20));
            Ui.Txt(g, a.detalhe, Ui.F(12), Ui.FG3, new Rectangle(r.X + 60, r.Y + 33, larg, 18));
            Ui.TxtDir(g, Calc.Brl(a.valor), Ui.F(15, true), a.cor,
                      new Rectangle(r.X, r.Y, r.Width - btn.Width - 30, r.Height));
        };
        Direita(l, btn, 12, 15);
        return l;
    }

    Card FaixaBackup() {
        string ult = Aplicacao.UltimoBackup;
        var c = new Card { Height = 92, Raio = 20, BackColor = Ui.BG };
        var btn = Bot("Gerar backup agora", "backup", string.IsNullOrEmpty(ult), () => {
            Aplicacao.ExportarBackup(S); Render();
        });
        c.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 22, r.Y + 26, 40, 40), "backup",
                    string.IsNullOrEmpty(ult) ? Ui.WARN : Ui.OK,
                    Ui.Alfa(string.IsNullOrEmpty(ult) ? Ui.WARN : Ui.OK, .12), 13);
            Ui.Txt(g, string.IsNullOrEmpty(ult) ? "Você ainda não gerou um backup"
                                                : "Último backup em " + Calc.Fmt(ult),
                   Ui.F(15, true), Ui.FG, new Rectangle(r.X + 74, r.Y + 24, r.Width - 300, 22));
            Ui.Txt(g, "O arquivo .mcb abre em qualquer computador com a senha que você escolher.",
                   Ui.F(12), Ui.FG3, new Rectangle(r.X + 74, r.Y + 46, r.Width - 300, 18));
        };
        Direita(c, btn, 22, 26);
        btn.BackColor = Ui.CARD;
        return c;
    }

    /* ---------------------------- Cartão · Resumo ---------------------------- */

    void VResumoCartao(List<Parcela> ps, List<TotalPor> totais, string ym) {
        var abertas = ps.Where(x => !x.pago).ToList();
        var fatura = ps.Where(x => Calc.MesDe(x.venc) == ym).ToList();
        double fatTotal = fatura.Sum(x => x.v);
        double fatAberto = fatura.Where(x => !x.pago).Sum(x => x.v);
        double emAberto = abertas.Sum(x => x.v);
        double pago = ps.Sum(x => x.v) - emAberto;
        string venc = VencFatura(ym);
        int dias = Calc.DiasAte(venc);
        int atrasadas = abertas.Count(x => Calc.DiasAte(x.venc) < 0);

        Cabecalho("resumo", "Cartão compartilhado",
                  "Fatura de " + Calc.MesLabel(ym) + " " +
                  (dias < 0 ? "venceu em " + Calc.Fmt(venc) : dias == 0 ? "vence hoje"
                   : dias == 1 ? "vence amanhã" : "vence em " + dias + " dias") +
                  " · " + Calc.Brl(emAberto) + " em aberto no total",
                  Bot("Nova compra", "mais", true, () => NovoLanc(null)),
                  Bot("Nova pessoa", "pessoa", false, () => NovaPessoa(null)));

        // ações rápidas, cada uma com o número real
        var semFone = S.people.Count(p => new string((p.fone ?? "").Where(char.IsDigit).ToArray()).Length < 10);
        var atalhos = Grade(88, 3, new List<Control> {
            Atalho("cobrar", "Cobrar no WhatsApp",
                   S.people.Count - semFone + " de " + S.people.Count + " com número cadastrado",
                   () => Ir("cartao", "Pessoas")),
            Atalho("check", "Marcar parcelas pagas",
                   abertas.Count + " parcelas em aberto",
                   () => { filtro = "abertas"; Ir("cartao", "Parcelas"); }),
            Atalho("alerta", "Ver o que está atrasado",
                   atrasadas == 0 ? "nenhuma parcela atrasada"
                                  : atrasadas + " parcela" + (atrasadas > 1 ? "s atrasadas" : " atrasada"),
                   () => { filtro = "atrasadas"; Ir("cartao", "Parcelas"); }),
        });

        // coluna da esquerda: a fatura do mês, dois KPIs e o limite
        var esq = NovaPilha();
        Add(esq, CardFatura(fatTotal, fatAberto, venc, dias, fatura.Count), 206);
        Add(esq, Grade(104, 2, new List<Control> {
            Kpi("Em aberto", Calc.Brl(emAberto), abertas.Count + " parcelas", emAberto > 0 ? Ui.WARN : Ui.OK),
            Kpi("Já pago", Calc.Brl(pago), ps.Count(x => x.pago) + " parcelas", Ui.OK),
        }), 104);
        if (S.cfg.limit > 0) Add(esq, CardLimite(emAberto), 118);

        // coluna da direita: o gráfico e quem deve
        var dir = NovaPilha();
        Add(dir, CardGrafico(ps, ym), 268);
        var linhas = totais.Select(t => (Control)LinhaQuemDeve(t, fatura, ym)).ToList();
        Add(dir, linhas.Count == 0
            ? (Control)Vazio("pessoas", "Ninguém deve nada", "Lance uma compra pra ver o rateio por pessoa.", null)
            : Lista(52, linhas, (g, r) => {
                  Ui.Txt(g, "Quem deve", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 22, r.Y + 16, 200, 22));
                  Ui.TxtDir(g, Calc.Brl(emAberto) + " no total", Ui.F(13), Ui.FG3,
                            new Rectangle(r.X, r.Y + 17, r.Width - 22, 20));
              }, 64),
            linhas.Count == 0 ? 220 : 52 + linhas.Count * 64 + 12);

        int altura = Math.Max(206 + 16 + 104 + (S.cfg.limit > 0 ? 16 + 118 : 0),
                              268 + 16 + (linhas.Count == 0 ? 220 : 52 + linhas.Count * 64 + 12));
        Pilha(atalhos, Colunas(altura, 42, esq, dir));
    }

    Card Atalho(string icone, string titulo, string sub, Action ir) {
        var c = new Card { Height = 88, Raio = 18, BackColor = Ui.BG };
        c.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 20, r.Y + 25, 38, 38), icone, Ui.ACC, Ui.CHIP, 12);
            Ui.Txt(g, titulo, Ui.F(15, true), Ui.FG, new Rectangle(r.X + 70, r.Y + 24, r.Width - 110, 20));
            Ui.Txt(g, sub, Ui.F(12), Ui.FG3, new Rectangle(r.X + 70, r.Y + 45, r.Width - 110, 18));
            Ui.Icone(g, "seta", new RectangleF(r.Right - 34, r.Y + 36, 16, 16), Ui.FG3);
        };
        c.Clicavel(ir);
        return c;
    }

    static Card Kpi(string rotulo, string valor, string rodape, Color cor) {
        return Bloco(104, 18, (g, r) => {
            Ui.Rotulo(g, rotulo, Ui.LBL, r.X + 22, r.Y + 22);
            Ui.TxtAjuste(g, valor, 28, cor, new Rectangle(r.X + 22, r.Y + 40, r.Width - 44, 34));
            Ui.Txt(g, rodape, Ui.F(12), Ui.FG3, new Rectangle(r.X + 22, r.Y + 74, r.Width - 44, 16));
        });
    }

    static Card CardFatura(double total, double aberto, string venc, int dias, int qtd) {
        double pct = total > 0 ? (total - aberto) / total * 100 : 100;
        return Bloco(206, 22, (g, r) => {
            Ui.Rotulo(g, "Fatura de " + Calc.MesLabel(Calc.MesDe(venc)), Ui.LBL, r.X + 26, r.Y + 26);
            Ui.TxtAjuste(g, Calc.Brl(total), 40, Ui.FG, new Rectangle(r.X + 26, r.Y + 46, r.Width - 52, 48));
            string pil = dias < 0 ? "VENCEU EM " + Calc.Fmt(venc) : dias == 0 ? "FECHA HOJE"
                       : dias == 1 ? "FECHA AMANHÃ" : "FECHA EM " + dias + " DIAS";
            Ui.Pilula(g, pil, dias <= 1 ? Ui.WARN : Ui.FG2, dias < 0 ? Ui.BADBG : dias <= 1 ? Ui.WARNBG : Ui.NEUBG,
                      r.X + 26, r.Y + 100, 26);
            Ui.Txt(g, qtd + (qtd == 1 ? " parcela no mês" : " parcelas no mês"), Ui.F(12), Ui.FG3,
                   new Rectangle(r.X + 26, r.Y + 136, r.Width - 52, 16));
            Ui.Progresso(g, new Rectangle(r.X + 26, r.Y + 158, r.Width - 52, 10), pct, Ui.ACC);
            Ui.Txt(g, Calc.Brl(total - aberto) + " já pago", Ui.F(12), Ui.FG2,
                   new Rectangle(r.X + 26, r.Y + 174, r.Width - 52, 18));
            Ui.TxtDir(g, Calc.Brl(aberto) + " falta", Ui.F(12, true), aberto > 0 ? Ui.WARN : Ui.OK,
                      new Rectangle(r.X, r.Y + 174, r.Width - 26, 18));
        });
    }

    Card CardLimite(double usado) {
        double pct = S.cfg.limit > 0 ? usado / S.cfg.limit * 100 : 0;
        return Bloco(118, 18, (g, r) => {
            Ui.Rotulo(g, "Limite do cartão", Ui.LBL, r.X + 22, r.Y + 22);
            Ui.Txt(g, Calc.Brl(usado), Ui.F(24, true), pct >= 80 ? Ui.BAD : Ui.FG,
                   new Rectangle(r.X + 22, r.Y + 40, r.Width - 44, 30));
            Ui.TxtDir(g, "de " + Calc.Brl(S.cfg.limit), Ui.F(13), Ui.FG3,
                      new Rectangle(r.X, r.Y + 46, r.Width - 22, 20));
            Ui.Progresso(g, new Rectangle(r.X + 22, r.Y + 76, r.Width - 44, 10), pct, pct >= 80 ? Ui.BAD : Ui.WARN);
            Ui.Txt(g, pct.ToString("F0") + "% comprometido · " + Calc.Brl(Math.Max(0, S.cfg.limit - usado)) + " livre",
                   Ui.F(12), Ui.FG3, new Rectangle(r.X + 22, r.Y + 90, r.Width - 44, 18));
        });
    }

    Card LinhaQuemDeve(TotalPor t, List<Parcela> fatura, string ym) {
        double naFatura = fatura.Where(y => y.pid == t.id && !y.pago).Sum(y => y.v);
        double maior = Math.Max(1, t.gasto);
        var cor = ColorTranslator.FromHtml(t.cor);
        var l = Linha(64, null);
        var btn = new Botao("", "cobrar") { Height = 34, Width = 42, BackColor = Ui.CARD };
        btn.Click += (o, e) => Cobrar(t, ym);
        l.Desenhar = (g, r) => {
            Ui.Avatar(g, new Rectangle(r.X + 12, r.Y + 14, 36, 36), t.nome, cor);
            Ui.Txt(g, t.nome, Ui.F(14, true), Ui.FG, new Rectangle(r.X + 58, r.Y + 12, r.Width - 280, 20));
            Ui.Txt(g, t.abertas + (t.abertas == 1 ? " parcela aberta" : " parcelas abertas") +
                      (naFatura > 0 ? " · " + Calc.Brl(naFatura) + " nesta fatura" : ""),
                   Ui.F(12), Ui.FG3, new Rectangle(r.X + 58, r.Y + 32, r.Width - 280, 16));
            Ui.Progresso(g, new Rectangle(r.X + 58, r.Y + 50, Math.Max(40, r.Width - 280), 5),
                         t.deve / maior * 100, cor);
            Ui.TxtDir(g, Calc.Brl(t.deve), Ui.F(15, true), t.deve > 0 ? Ui.FG : Ui.OK,
                      new Rectangle(r.X, r.Y + 14, r.Width - 66, 20));
            Ui.TxtDir(g, "em aberto", Ui.F(11), Ui.FG3, new Rectangle(r.X, r.Y + 36, r.Width - 66, 16));
        };
        Direita(l, btn, 12, 15);
        return l;
    }

    void Cobrar(TotalPor x, string ym) {
        var pessoa = S.people.FirstOrDefault(y => y.id == x.id);
        string fone = pessoa == null ? "" : new string((pessoa.fone ?? "").Where(char.IsDigit).ToArray());
        if (fone.Length < 10) {
            Aplicacao.Erro("Essa pessoa não tem WhatsApp cadastrado.\n\nAbra Pessoas e adicione o número.");
            return;
        }
        double fatura = Calc.Parcelas(S.buys, S.paid)
            .Where(y => y.pid == x.id && !y.pago && Calc.MesDe(y.venc) == ym).Sum(y => y.v);
        string msg = string.Format("Oi {0}! Na fatura de {1} (vence {2}) ficaram {3} pra você. Total em aberto: {4}.",
            x.nome, Calc.MesLabel(ym), Calc.Fmt(VencFatura(ym)), Calc.Brl(fatura), Calc.Brl(x.deve));
        try { Process.Start("https://wa.me/55" + fone + "?text=" + Uri.EscapeDataString(msg)); }
        catch (Exception e) { Aplicacao.Erro("Não deu pra abrir o WhatsApp: " + e.Message); }
    }

    /* ---------------------------- gráfico de vencimentos ---------------------------- */

    class Barra { public string mes; public List<KeyValuePair<Color, double>> partes; public double total; }

    List<Barra> Barras(List<Parcela> ps, string ym, bool soAbertas, int quantos) {
        var cols = Colunas();
        var meses = ps.Select(x => Calc.MesDe(x.venc)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (quantos > 0 && meses.Count > quantos) {
            int i = Math.Max(0, meses.IndexOf(ym) - 1);
            meses = meses.Skip(Math.Min(i, Math.Max(0, meses.Count - quantos))).Take(quantos).ToList();
        }
        return meses.Select(m => {
            var partes = cols.Select(c => new KeyValuePair<Color, double>(
                ColorTranslator.FromHtml(c.cor),
                ps.Where(x => Calc.MesDe(x.venc) == m && x.pid == c.id && (!soAbertas || !x.pago)).Sum(x => x.v)))
                .Where(kv => kv.Value > 0).ToList();
            return new Barra { mes = m, partes = partes, total = partes.Sum(kv => kv.Value) };
        }).ToList();
    }

    Card CardGrafico(List<Parcela> ps, string ym) {
        var bs = Barras(ps, ym, true, 9);
        return Bloco(268, 20, (g, r) => {
            Ui.Txt(g, "Vencimentos mês a mês", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 24, r.Y + 20, 300, 24));
            Ui.TxtDir(g, "só o que está em aberto", Ui.F(12), Ui.FG3,
                      new Rectangle(r.X, r.Y + 22, r.Width - 24, 20));
            Grafico(g, new Rectangle(r.X + 24, r.Y + 60, r.Width - 48, r.Height - 84), bs, ym);
        });
    }

    static void Grafico(Graphics g, Rectangle r, List<Barra> bs, string ym) {
        if (bs.Count == 0 || bs.All(b => b.total <= 0)) {
            Ui.TxtCentro(g, "Nenhuma parcela em aberto.", Ui.F(13), Ui.FG3, r);
            return;
        }
        int baseY = r.Bottom - 26, topo = r.Y + 16;
        double max = Math.Max(1, bs.Max(b => b.total));
        int n = bs.Count;
        int passo = r.Width / n, larg = Math.Min(44, Math.Max(10, passo - 14));

        for (int i = 0; i < n; i++) {
            var b = bs[i];
            bool vencido = string.Compare(b.mes, ym, StringComparison.Ordinal) < 0;
            bool atual = b.mes == ym;
            int x = r.X + i * passo + (passo - larg) / 2;
            int h = (int)Math.Round((baseY - topo) * (b.total / max));
            var col = new Rectangle(x, baseY - Math.Max(h, b.total > 0 ? 4 : 0), larg, Math.Max(h, b.total > 0 ? 4 : 0));

            if (col.Height > 0) {
                using (var caminho = Ui.Round(col, 8)) {
                    var st = g.Save();
                    g.SetClip(caminho);
                    int y = col.Bottom;
                    foreach (var parte in b.partes) {
                        int ph = (int)Math.Round(col.Height * (parte.Value / b.total));
                        // mês vencido inteiro em vermelho: quem olha precisa ver o atraso, não o rateio
                        Ui.Fill(g, new Rectangle(col.X, y - ph, col.Width, ph), 0,
                                vencido ? Ui.BAD : parte.Key);
                        y -= ph;
                    }
                    if (y > col.Y) Ui.Fill(g, new Rectangle(col.X, col.Y, col.Width, y - col.Y), 0,
                                           vencido ? Ui.BAD : Ui.LINE2);
                    g.Restore(st);
                }
            }
            Ui.TxtCentro(g, Calc.MesLabel(b.mes).Substring(0, 3), Ui.F(11, atual),
                         atual ? Ui.ACC : vencido ? Ui.BAD : Ui.FG3,
                         new Rectangle(r.X + i * passo, baseY + 6, passo, 16));
        }
        Ui.Divisoria(g, r.X, baseY + 1, r.Width);
    }

    /* ---------------------------- Por mês ---------------------------- */

    void VPorMes(List<Parcela> ps, string ym) {
        var todos = ps.Select(x => Calc.MesDe(x.venc)).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        var meses = todos.Where(m => !soFuturo || string.Compare(m, ym, StringComparison.Ordinal) >= 0).ToList();
        double aberto = ps.Where(x => !x.pago && meses.Contains(Calc.MesDe(x.venc))).Sum(x => x.v);

        var alterna = Bot(soFuturo ? "Mostrando só daqui pra frente" : "Esconder meses passados",
                          "calendario", soFuturo, () => { soFuturo = !soFuturo; Render(); });
        alterna.Pilula = true;
        Cabecalho("calendario", "Por mês",
                  meses.Count + (meses.Count == 1 ? " mês com parcelas · " : " meses com parcelas · ") +
                  Calc.Brl(aberto) + " em aberto", alterna);

        if (meses.Count == 0) {
            Pilha(Vazio("calendario", "Sem parcelas nesta faixa",
                        soFuturo ? "Todas as parcelas venceram antes deste mês. Mostre os meses passados pra vê-las."
                                 : "Lance uma compra ou uma dívida pra ver os vencimentos distribuídos por mês.",
                        soFuturo ? Bot("Mostrar meses passados", "calendario", true,
                                       () => { soFuturo = false; Render(); })
                                 : Bot(Cartao ? "Nova compra" : "Nova dívida", "mais", true, () => NovoLanc(null))));
            return;
        }

        var blocos = new List<Control> { CardGraficoMes(ps, ym, meses) };
        foreach (var m in meses) {
            string dessa = m;
            var doMes = ps.Where(x => Calc.MesDe(x.venc) == dessa).ToList();
            var cols = Colunas().Where(c => doMes.Any(x => x.pid == c.id)).ToList();
            double somaAberta = doMes.Where(x => !x.pago).Sum(x => x.v);
            bool vencido = string.Compare(dessa, ym, StringComparison.Ordinal) < 0 && somaAberta > 0;
            double maior = Math.Max(1, cols.Max(c => doMes.Where(x => x.pid == c.id).Sum(x => x.v)));

            var linhas = cols.Select(c => {
                var minhas = doMes.Where(x => x.pid == c.id).ToList();
                double ab = minhas.Where(x => !x.pago).Sum(x => x.v), tot = minhas.Sum(x => x.v);
                var cor = ColorTranslator.FromHtml(c.cor);
                return (Control)Linha(56, (g, r) => {
                    Ui.Avatar(g, new Rectangle(r.X + 12, r.Y + 12, 32, 32), c.nome, cor);
                    Ui.Txt(g, c.nome, Ui.F(14, true), Ui.FG, new Rectangle(r.X + 54, r.Y + 10, r.Width - 260, 18));
                    Ui.Txt(g, minhas.Count + (minhas.Count == 1 ? " parcela" : " parcelas") +
                              (ab <= 0 ? " · tudo pago" : ""),
                           Ui.F(12), ab <= 0 ? Ui.OK : Ui.FG3, new Rectangle(r.X + 54, r.Y + 29, r.Width - 260, 16));
                    Ui.Progresso(g, new Rectangle(r.Right - 190, r.Y + 26, 90, 5), tot / maior * 100, cor);
                    // quitado sai em cinza: número colorido ao lado de "tudo pago" lê como dívida
                    Ui.TxtDir(g, ab > 0 ? Calc.Brl(ab) : Calc.Brl(tot), Ui.F(15, true),
                              ab <= 0 ? Ui.FG3 : vencido ? Ui.BAD : Ui.FG,
                              new Rectangle(r.X, r.Y, r.Width - 14, r.Height));
                });
            }).ToList();

            blocos.Add(Lista(56, linhas, (g, r) => {
                Ui.Txt(g, Calc.MesLabel(dessa), Ui.F(16, true), dessa == ym ? Ui.ACC : Ui.FG,
                       new Rectangle(r.X + 22, r.Y + 18, 180, 22));
                int px = r.X + 22 + 110;
                if (dessa == ym) px += Ui.Pilula(g, "MÊS ATUAL", Ui.ACC, Ui.Alfa(Ui.ACC, .12), px, r.Y + 18, 22) + 8;
                else if (vencido) px += Ui.Pilula(g, "VENCIDO", Ui.BAD, Ui.BADBG, px, r.Y + 18, 22) + 8;
                Ui.TxtDir(g, doMes.Count + (doMes.Count == 1 ? " parcela · " : " parcelas · ") +
                             (somaAberta > 0 ? Calc.Brl(somaAberta) + " em aberto" : "tudo pago"),
                          Ui.F(13), somaAberta > 0 ? Ui.FG2 : Ui.OK,
                          new Rectangle(r.X, r.Y + 19, r.Width - 22, 20));
            }, 56));
        }
        Pilha(blocos.ToArray());
    }

    Card CardGraficoMes(List<Parcela> ps, string ym, List<string> meses) {
        var bs = Barras(ps.Where(x => meses.Contains(Calc.MesDe(x.venc))).ToList(), ym, true, 14);
        return Bloco(280, 22, (g, r) => {
            Ui.Txt(g, "Vencimentos mês a mês", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 26, r.Y + 22, 360, 24));
            Ui.TxtDir(g, "cada faixa é " + (Cartao ? "uma pessoa" : "um credor") + " · em vermelho, o que venceu",
                      Ui.F(12), Ui.FG3, new Rectangle(r.X, r.Y + 24, r.Width - 26, 20));
            Grafico(g, new Rectangle(r.X + 26, r.Y + 62, r.Width - 52, r.Height - 86), bs, ym);
        });
    }

    /* ---------------------------- Parcelas ---------------------------- */

    void VParcelas(List<Parcela> ps) {
        Func<Parcela, bool> passa = x =>
            filtro == "todas" ? true :
            filtro == "pagas" ? x.pago :
            filtro == "atrasadas" ? !x.pago && Calc.DiasAte(x.venc) < 0 : !x.pago;
        var rows = ps.Where(passa).ToList();
        int atrasadas = ps.Count(x => !x.pago && Calc.DiasAte(x.venc) < 0);

        Cabecalho("parcelas", "Parcelas",
                  ps.Count(x => !x.pago) + " em aberto · " + atrasadas + " atrasada" + (atrasadas == 1 ? "" : "s") +
                  " · um clique no círculo marca ou desmarca",
                  Bot(Cartao ? "Nova compra" : "Nova dívida", "mais", true, () => NovoLanc(null)));

        var filtros = BarraFiltros(rows.Count, rows.Sum(x => x.v));

        if (rows.Count == 0) {
            Pilha(filtros, Vazio("parcelas", "Nenhuma parcela " + filtro,
                filtro == "atrasadas" ? "Nada atrasado por aqui — é o estado que você quer."
                : filtro == "pagas" ? "Nenhuma parcela foi marcada como paga ainda. Clique no círculo de uma linha."
                : "Todas as parcelas já foram pagas.",
                Bot("Ver todas", null, true, () => { filtro = "todas"; Render(); })));
            return;
        }

        var blocos = new List<Control> { filtros };
        foreach (var grupo in rows.GroupBy(x => Calc.MesDe(x.venc)).OrderBy(x => x.Key, StringComparer.Ordinal)) {
            var doMes = grupo.ToList();
            string m = grupo.Key;
            var linhas = doMes.Select(x => (Control)LinhaParcela(x)).ToList();
            blocos.Add(Lista(56, linhas, (g, r) => {
                Ui.Txt(g, Calc.MesLabel(m), Ui.F(16, true), m == Mes ? Ui.ACC : Ui.FG,
                       new Rectangle(r.X + 22, r.Y + 18, 200, 22));
                Ui.TxtDir(g, doMes.Count + (doMes.Count == 1 ? " parcela · " : " parcelas · ") +
                             Calc.Brl(doMes.Sum(x => x.v)), Ui.F(13), Ui.FG3,
                          new Rectangle(r.X, r.Y + 19, r.Width - 22, 20));
            }, 56));
        }
        Pilha(blocos.ToArray());
    }

    Card BarraFiltros(int qtd, double soma) {
        var c = new Card {
            Height = 46, BackColor = Ui.BG, Fundo = Color.Transparent, Borda = Color.Transparent,
        };
        int x = 0;
        foreach (var f in new[] { "abertas", "atrasadas", "pagas", "todas" }) {
            string dessa = f;
            var b = Bot(dessa, null, false, () => { filtro = dessa; Render(); });
            b.Pilula = true; b.Ativo = dessa == filtro; b.Height = 38; b.Font = Ui.F(13, true); b.Medir();
            b.Location = new Point(x, 4);
            x += b.Width + 8;
            c.Controls.Add(b);
        }
        c.Desenhar = (g, r) => Ui.TxtDir(g, qtd + (qtd == 1 ? " parcela · " : " parcelas · ") + Calc.Brl(soma),
                                         Ui.F(13), Ui.FG2, new Rectangle(r.X, r.Y, r.Width, r.Height));
        return c;
    }

    Card LinhaParcela(Parcela x) {
        Color fg, bg;
        string st = Status(x, out fg, out bg);
        var cor = CorDe(x.pid);
        string quem = NomeDe(x.pid);
        var l = Linha(56, null);
        l.Desenhar = (g, r) => {
            // círculo de marcação: um clique resolve, sem clique duplo escondido
            var circ = new Rectangle(r.X + 14, r.Y + 16, 24, 24);
            if (x.pago) {
                Ui.Fill(g, circ, 12, Ui.ACC);
                Ui.Icone(g, "check", new RectangleF(circ.X + 5, circ.Y + 5, 14, 14), Ui.ONACC);
            } else {
                Ui.Borda(g, circ, 12, Ui.LINE2, 2);
            }
            int larg = r.Width - 52 - 560;
            Ui.Txt(g, x.nome, Ui.F(14, true), x.pago ? Ui.FG3 : Ui.FG, new Rectangle(r.X + 52, r.Y + 10, larg, 18));
            if (x.pago) {
                int w = Math.Min(larg, Ui.Larg(g, x.nome, Ui.F(14, true)));
                using (var p = new Pen(Ui.FG3)) g.DrawLine(p, r.X + 52, r.Y + 19, r.X + 52 + w, r.Y + 19);
            }
            Ui.Txt(g, x.rec ? "assinatura · " + Calc.MesLabel(Calc.MesDe(x.venc)) : "parcela " + x.i + " de " + x.n,
                   Ui.F(12), Ui.FG3, new Rectangle(r.X + 52, r.Y + 29, larg, 16));

            Ui.Avatar(g, new Rectangle(r.Right - 540, r.Y + 15, 26, 26), quem, cor);
            Ui.Txt(g, quem, Ui.F(13), Ui.FG2, new Rectangle(r.Right - 508, r.Y, 112, r.Height));
            Ui.Txt(g, Calc.Fmt(x.venc), Ui.F(13), Ui.FG2, new Rectangle(r.Right - 388, r.Y, 88, r.Height));
            Ui.PilulaDir(g, st, fg, bg, r.Right - 150, r.Y + 16, 24);
            Ui.TxtDir(g, Calc.Brl(x.v), Ui.F(15, true), x.pago ? Ui.FG3 : Ui.FG,
                      new Rectangle(r.X, r.Y, r.Width - 14, r.Height));
        };
        l.Clicavel(() => Alternar(x.chave));
        return l;
    }

    void Alternar(string chave) {
        var pagos = Pagos;
        if (pagos.ContainsKey(chave)) pagos.Remove(chave);
        else pagos[chave] = Calc.Hoje();
        Gravar();
        Render();
    }

    /* ---------------------------- Compras ---------------------------- */

    void VCompras() {
        var ps = Calc.Parcelas(S.buys, S.paid);
        double total = S.buys.Sum(b => b.rec ? ps.Where(x => x.lancId == b.id).Sum(x => x.v) : b.total);

        Cabecalho("compras", "Compras",
                  S.buys.Count + (S.buys.Count == 1 ? " compra lançada · " : " compras lançadas · ") +
                  Calc.Brl(total) + " no total",
                  Bot("Nova compra", "mais", true, () => NovoLanc(null)));

        if (S.buys.Count == 0) {
            Pilha(Vazio("compras", "Nenhuma compra lançada",
                        "Lance a primeira compra: escolha a pessoa, o valor e em quantas vezes. " +
                        "As parcelas e os vencimentos saem daí.",
                        Bot("Nova compra", "mais", true, () => NovoLanc(null))));
            return;
        }

        var linhas = S.buys.OrderByDescending(x => x.date, StringComparer.Ordinal)
                           .Select(b => (Control)LinhaLanc(b, ps, true)).ToList();
        Pilha(Lista(56, linhas, (g, r) => {
            Ui.Txt(g, "Todas as compras", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 22, r.Y + 17, 300, 24));
            Ui.TxtDir(g, "da mais recente para a mais antiga", Ui.F(13), Ui.FG3,
                      new Rectangle(r.X, r.Y + 19, r.Width - 22, 20));
        }, 72));
    }

    Card LinhaLanc(Lanc b, List<Parcela> ps, bool cartao) {
        var minhas = ps.Where(x => x.lancId == b.id).ToList();
        double pago = minhas.Where(x => x.pago).Sum(x => x.v);
        double tot = b.rec ? minhas.Sum(x => x.v) : b.total;
        var vals = Calc.Dividir(b.total, Math.Max(1, b.n));
        var cor = CorDe(b.pid);
        string quem = NomeDe(b.pid);
        string cond = b.rec
            ? Calc.Brl(b.total) + " por mês" + (string.IsNullOrEmpty(b.ate) ? "" : " até " + Calc.MesLabel(b.ate))
            : b.n + "x de " + Calc.Brl(vals[0]);

        var l = Linha(72, null);
        var editar = new Botao("", "editar") { Height = 34, Width = 40 };
        editar.Click += (o, e) => NovoLanc(b);
        var excluir = new Botao("", "excluir") { Height = 34, Width = 40, Perigo = true };
        excluir.Click += (o, e) => ExcluirLanc(b);

        l.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 12, r.Y + 18, 38, 38), Ui.IconeCategoria(b.cat), Ui.ICO, Ui.FIELD, 12);
            int larg = r.Width - 62 - 540;
            Ui.Txt(g, b.name, Ui.F(14, true), Ui.FG, new Rectangle(r.X + 62, r.Y + 18, larg, 18));
            Ui.Txt(g, (b.rec ? "repete todo mês" : b.cat) + " · " + Calc.Fmt(b.date), Ui.F(12), Ui.FG3,
                   new Rectangle(r.X + 62, r.Y + 38, larg, 16));

            Ui.Avatar(g, new Rectangle(r.Right - 530, r.Y + 23, 26, 26), quem, cor);
            Ui.Txt(g, quem, Ui.F(13), Ui.FG2, new Rectangle(r.Right - 498, r.Y, 110, r.Height));

            Ui.Txt(g, cond, Ui.F(13), Ui.FG2, new Rectangle(r.Right - 380, r.Y + 14, 150, 18));
            Ui.Progresso(g, new Rectangle(r.Right - 380, r.Y + 36, 150, 5),
                         tot > 0 ? pago / tot * 100 : 0, Ui.ACC);
            Ui.Txt(g, Calc.Brl(pago) + " pago", Ui.F(11), Ui.FG3, new Rectangle(r.Right - 380, r.Y + 44, 150, 16));

            Ui.TxtDir(g, Calc.Brl(tot), Ui.F(15, true), Ui.FG, new Rectangle(r.X, r.Y, r.Width - 110, r.Height));
        };
        Direita(l, excluir, 12, 19);
        Direita(l, editar, 58, 19);
        return l;
    }

    /* ---------------------------- Pessoas ---------------------------- */

    void VPessoas(List<TotalPor> totais, string ym) {
        var ps = Calc.Parcelas(S.buys, S.paid);
        var fatura = ps.Where(x => Calc.MesDe(x.venc) == ym && !x.pago).ToList();

        Cabecalho("pessoas", "Pessoas",
                  S.people.Count + (S.people.Count == 1 ? " pessoa · " : " pessoas · ") +
                  Calc.Brl(totais.Sum(t => t.deve)) + " em aberto no total",
                  Bot("Nova pessoa", "mais", true, () => NovaPessoa(null)));

        double maior = Math.Max(1, totais.Count == 0 ? 1 : totais.Max(t => t.deve));
        var cards = S.people.Select(p => {
            var t = totais.FirstOrDefault(x => x.id == p.id) ?? new TotalPor { id = p.id, nome = p.nome, cor = p.cor };
            return (Control)CardPessoa(p, t, fatura.Where(x => x.pid == p.id).Sum(x => x.v), maior);
        }).ToList();

        Pilha(Grade(238, 3, cards));
    }

    Card CardPessoa(Pessoa p, TotalPor t, double naFatura, double maior) {
        var cor = ColorTranslator.FromHtml(p.cor);
        bool temFone = new string((p.fone ?? "").Where(char.IsDigit).ToArray()).Length >= 10;
        int compras = S.buys.Count(b => b.pid == p.id);

        var c = new Card { Height = 238, Raio = 20, BackColor = Ui.BG };
        c.Desenhar = (g, r) => {
            Ui.Avatar(g, new Rectangle(r.X + 24, r.Y + 24, 52, 52), p.nome, cor);
            Ui.Txt(g, p.nome, Ui.F(17, true), Ui.FG, new Rectangle(r.X + 88, r.Y + 30, r.Width - 112, 22));
            Ui.Txt(g, temFone ? p.fone : "sem WhatsApp cadastrado", Ui.F(12), temFone ? Ui.FG3 : Ui.WARN,
                   new Rectangle(r.X + 88, r.Y + 52, r.Width - 112, 18));
            Ui.Rotulo(g, "Em aberto", Ui.LBL, r.X + 24, r.Y + 88);
            Ui.TxtAjuste(g, Calc.Brl(t.deve), 32, t.deve > 0 ? Ui.FG : Ui.OK,
                         new Rectangle(r.X + 24, r.Y + 104, r.Width - 48, 38));
            Ui.Progresso(g, new Rectangle(r.X + 24, r.Y + 146, r.Width - 48, 6), t.deve / maior * 100, cor);
            Ui.Txt(g, compras + (compras == 1 ? " compra · " : " compras · ") +
                      (naFatura > 0 ? Calc.Brl(naFatura) + " nesta fatura" : "nada nesta fatura"),
                   Ui.F(12), Ui.FG3, new Rectangle(r.X + 24, r.Y + 160, r.Width - 48, 18));
        };
        var cobrar = Bot("Cobrar", "cobrar", true, () => Cobrar(t, Mes));
        cobrar.Height = 40; cobrar.Medir();
        cobrar.Location = new Point(24, 186);
        var editar = new Botao("", "editar") { Height = 40, Width = 44, BackColor = Ui.CARD };
        editar.Click += (o, e) => NovaPessoa(p);
        c.Controls.Add(cobrar);
        cobrar.BackColor = Ui.CARD;
        Direita(c, editar, 24, 186);
        return c;
    }

    /* ---------------------------- Dívidas · Resumo ---------------------------- */

    void VResumoDividas(List<Parcela> ps, List<TotalPor> totais, string ym) {
        var abertas = ps.Where(x => !x.pago).ToList();
        double total = ps.Sum(x => x.v), falta = abertas.Sum(x => x.v), pago = total - falta;
        var atrasadas = abertas.Where(x => Calc.DiasAte(x.venc) < 0).ToList();
        var doMes = ps.Where(x => Calc.MesDe(x.venc) == ym && !x.pago).ToList();
        double pct = total > 0 ? pago / total * 100 : 100;
        string ultima = ps.Count > 0 ? ps.Max(x => x.venc) : "";

        Cabecalho("resumo", "Dívidas pessoais",
                  Calc.Brl(falta) + " a pagar · " +
                  (atrasadas.Count > 0 ? atrasadas.Count + " parcela" + (atrasadas.Count > 1 ? "s atrasadas" : " atrasada")
                                       : "nada atrasado") +
                  " · " + Calc.Brl(doMes.Sum(x => x.v)) + " vence em " + Calc.MesLabel(ym),
                  Bot("Nova dívida", "mais", true, () => NovoLanc(null)));

        var kpis = Grade(130, 3, new List<Control> {
            Kpi130("Já paguei", Calc.Brl(pago), ps.Count(x => x.pago) + " parcelas quitadas", Ui.OK, false),
            Kpi130("Falta pagar", Calc.Brl(falta), abertas.Count + " parcelas em aberto", Ui.FG, false),
            Kpi130("Atrasado", Calc.Brl(atrasadas.Sum(x => x.v)),
                   atrasadas.Count == 0 ? "nenhuma parcela vencida"
                       : "a mais antiga venceu em " + Calc.Fmt(atrasadas.Min(x => x.venc)),
                   atrasadas.Count > 0 ? Ui.BAD : Ui.OK, atrasadas.Count > 0),
        });

        var progresso = Bloco(118, 20, (g, r) => {
            Ui.Txt(g, "Progresso de quitação", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 26, r.Y + 22, 300, 24));
            Ui.TxtDir(g, Calc.Brl(pago) + " de " + Calc.Brl(total) + " · " + pct.ToString("F0") + "%",
                      Ui.F(13), Ui.FG2, new Rectangle(r.X, r.Y + 24, r.Width - 26, 20));
            Ui.Progresso(g, new Rectangle(r.X + 26, r.Y + 60, r.Width - 52, 12), pct, Ui.OK);
            Ui.Txt(g, string.IsNullOrEmpty(ultima) ? "sem parcelas cadastradas"
                      : "a última parcela cai em " + Calc.Fmt(ultima),
                   Ui.F(12), Ui.FG3, new Rectangle(r.X + 26, r.Y + 80, r.Width - 52, 18));
        });

        var credores = totais.Select(t => {
            var cor = ColorTranslator.FromHtml(t.cor);
            double noMes = doMes.Where(x => x.pid == t.id).Sum(x => x.v);
            double maior = Math.Max(1, totais.Max(y => y.deve));
            return (Control)Linha(64, (g, r) => {
                Ui.Chip(g, new Rectangle(r.X + 12, r.Y + 14, 36, 36), "carteira", cor, Ui.Alfa(cor, .14), 12);
                Ui.Txt(g, t.nome, Ui.F(14, true), Ui.FG, new Rectangle(r.X + 58, r.Y + 12, r.Width - 260, 20));
                Ui.Txt(g, t.abertas + (t.abertas == 1 ? " parcela aberta" : " parcelas abertas") +
                          (noMes > 0 ? " · " + Calc.Brl(noMes) + " neste mês" : ""),
                       Ui.F(12), Ui.FG3, new Rectangle(r.X + 58, r.Y + 32, r.Width - 260, 16));
                Ui.Progresso(g, new Rectangle(r.X + 58, r.Y + 50, Math.Max(40, r.Width - 260), 5),
                             t.deve / maior * 100, cor);
                Ui.TxtDir(g, Calc.Brl(t.deve), Ui.F(15, true), t.deve > 0 ? Ui.FG : Ui.OK,
                          new Rectangle(r.X, r.Y + 14, r.Width - 14, 20));
                Ui.TxtDir(g, "em aberto", Ui.F(11), Ui.FG3, new Rectangle(r.X, r.Y + 36, r.Width - 14, 16));
            });
        }).ToList();

        var proximas = abertas.Take(6).Select(x => {
            Color fg, bg;
            string st = Status(x, out fg, out bg);
            return (Control)Linha(56, (g, r) => {
                Ui.Marca(g, new Rectangle(r.X + 12, r.Y + 12, 4, 32), fg);
                Ui.Txt(g, x.nome, Ui.F(14, true), Ui.FG, new Rectangle(r.X + 28, r.Y + 10, r.Width - 260, 18));
                // a data vem antes: se faltar espaço, quem some é o número da parcela
                Ui.Txt(g, Calc.Fmt(x.venc) + " · " + x.pid + " · " +
                          (x.rec ? "assinatura" : "parcela " + x.i + " de " + x.n),
                       Ui.F(12), Ui.FG3, new Rectangle(r.X + 28, r.Y + 29, r.Width - 260, 16));
                Ui.PilulaDir(g, st, fg, bg, r.Right - 118, r.Y + 16, 24);
                Ui.TxtDir(g, Calc.Brl(x.v), Ui.F(15, true), Ui.FG, new Rectangle(r.X, r.Y, r.Width - 14, r.Height));
            });
        }).ToList();

        var esq = NovaPilha();
        Add(esq, credores.Count == 0
            ? (Control)Vazio("carteira", "Nenhum credor", "Cadastre uma dívida pra ver o rateio por credor.", null)
            : Lista(52, credores, (g, r) => {
                  Ui.Txt(g, "Para quem eu devo", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 22, r.Y + 16, 260, 22));
                  Ui.TxtDir(g, totais.Count + (totais.Count == 1 ? " credor" : " credores"), Ui.F(13), Ui.FG3,
                            new Rectangle(r.X, r.Y + 17, r.Width - 22, 20));
              }, 64), credores.Count == 0 ? 220 : 52 + credores.Count * 64 + 12);

        var dir = NovaPilha();
        Add(dir, proximas.Count == 0
            ? (Control)Vazio("check", "Nada em aberto", "Todas as parcelas cadastradas já foram pagas.", null)
            : Lista(52, proximas, (g, r) => {
                  Ui.Txt(g, "Próximas parcelas", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 22, r.Y + 16, 260, 22));
                  Ui.TxtDir(g, "por vencimento", Ui.F(13), Ui.FG3,
                            new Rectangle(r.X, r.Y + 17, r.Width - 22, 20));
              }, 56), proximas.Count == 0 ? 220 : 52 + proximas.Count * 56 + 12);

        int alt = Math.Max(credores.Count == 0 ? 220 : 52 + credores.Count * 64 + 12,
                           proximas.Count == 0 ? 220 : 52 + proximas.Count * 56 + 12);
        Pilha(kpis, progresso, Colunas(alt, 50, esq, dir));
    }

    static Card Kpi130(string rotulo, string valor, string rodape, Color cor, bool perigo) {
        var c = Bloco(130, 20, null);
        if (perigo) { c.Fundo = Ui.RUIM_FUNDO; c.Grad = Ui.CARD; c.Borda = Ui.RUIM_BORDA; }
        c.Desenhar = (g, r) => {
            Ui.Rotulo(g, rotulo, Ui.LBL, r.X + 24, r.Y + 26);
            Ui.TxtAjuste(g, valor, 28, cor, new Rectangle(r.X + 24, r.Y + 46, r.Width - 48, 36));
            Ui.Txt(g, rodape, Ui.F(12), Ui.FG3, new Rectangle(r.X + 24, r.Y + 88, r.Width - 48, 18));
        };
        return c;
    }

    /* ---------------------------- Dívidas · lista ---------------------------- */

    void VDividas() {
        var ps = Calc.Parcelas(S.debts, S.dpaid);
        double aberto = ps.Where(x => !x.pago).Sum(x => x.v);

        Cabecalho("carteira", "Dívidas",
                  S.debts.Count + (S.debts.Count == 1 ? " dívida · " : " dívidas · ") +
                  Calc.Brl(aberto) + " em aberto",
                  Bot("Nova dívida", "mais", true, () => NovoLanc(null)));

        var cards = S.debts.OrderByDescending(x => x.first, StringComparer.Ordinal)
                           .Select(d => (Control)CardDivida(d, ps)).ToList();
        Pilha(cards.ToArray());
    }

    Card CardDivida(Lanc d, List<Parcela> ps) {
        var minhas = ps.Where(x => x.lancId == d.id).ToList();
        int pagas = minhas.Count(x => x.pago), qtd = minhas.Count;
        double totalGerado = minhas.Sum(x => x.v);
        double emAberto = minhas.Where(x => !x.pago).Sum(x => x.v);
        var vals = Calc.Dividir(d.total, Math.Max(1, d.n));
        var cor = ColorTranslator.FromHtml(Calc.CorDoNome(d.pid));
        string cond = d.rec
            ? Calc.Brl(d.total) + " por mês desde " + Calc.MesLabel(Calc.MesDe(d.first)) +
              (string.IsNullOrEmpty(d.ate) ? "" : " até " + Calc.MesLabel(d.ate))
            : d.n + "x de " + Calc.Brl(vals[0]) + " · 1ª em " + Calc.Fmt(d.first);
        string ultima = minhas.Count > 0 ? Calc.Fmt(minhas.Max(x => x.venc)) : "—";

        var c = new Card { Height = 158, Raio = 20, BackColor = Ui.BG };
        c.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 24, r.Y + 24, 44, 44), "carteira", cor, Ui.Alfa(cor, .14), 13);
            int larg = r.Width - 84 - 300;
            Ui.Txt(g, d.name, Ui.F(17, true), Ui.FG, new Rectangle(r.X + 84, r.Y + 26, larg, 22));
            Ui.Txt(g, d.pid + " · " + d.cat + " · " + cond, Ui.F(13), Ui.FG3,
                   new Rectangle(r.X + 84, r.Y + 50, larg, 20));
            Ui.TxtDir(g, Calc.Brl(emAberto), Ui.F(24, true), emAberto > 0 ? Ui.FG : Ui.OK,
                      new Rectangle(r.X, r.Y + 26, r.Width - 132, 30));
            Ui.TxtDir(g, "de " + Calc.Brl(totalGerado) + " no total", Ui.F(12), Ui.FG3,
                      new Rectangle(r.X, r.Y + 58, r.Width - 132, 18));

            Ui.Progresso(g, new Rectangle(r.X + 24, r.Y + 104, r.Width - 48, 10),
                         qtd > 0 ? pagas * 100.0 / qtd : 0, Ui.OK);
            Ui.Txt(g, pagas + " de " + qtd + (qtd == 1 ? " parcela paga" : " parcelas pagas"),
                   Ui.F(12), Ui.FG2, new Rectangle(r.X + 24, r.Y + 120, r.Width / 2, 18));
            Ui.TxtDir(g, d.rec ? "assinatura sem fim definido" : "última em " + ultima, Ui.F(12), Ui.FG3,
                      new Rectangle(r.X, r.Y + 120, r.Width - 24, 18));
        };
        var editar = new Botao("", "editar") { Height = 36, Width = 42 };
        editar.Click += (o, e) => NovoLanc(d);
        var excluir = new Botao("", "excluir") { Height = 36, Width = 42, Perigo = true };
        excluir.Click += (o, e) => ExcluirLanc(d);
        Direita(c, excluir, 24, 26);
        Direita(c, editar, 74, 26);
        return c;
    }

    /* ---------------------------- Configurações ---------------------------- */

    void VConfig() {
        Cabecalho("config", "Configurações",
                  "Cartão, backup e seus dados · MoneyControl v" + Aplicacao.VERSAO);

        var due = new NumericUpDown {
            Minimum = 1, Maximum = 28, Value = S.cfg.due, BackColor = Ui.FIELD, ForeColor = Ui.FG,
            BorderStyle = BorderStyle.None, Font = Ui.F(15), Width = 120, Location = new Point(26, 92),
        };
        var limite = new NumericUpDown {
            Maximum = 9999999, DecimalPlaces = 2, Increment = 100, Value = (decimal)S.cfg.limit,
            BackColor = Ui.FIELD, ForeColor = Ui.FG, BorderStyle = BorderStyle.None, Font = Ui.F(15),
            Width = 160, Location = new Point(26, 168),
        };
        EventHandler grava = (o, e) => {
            S.cfg.due = (int)due.Value;
            S.cfg.limit = (double)limite.Value;
            Gravar();
        };
        due.ValueChanged += grava;
        limite.ValueChanged += grava;
        // as setinhas do NumericUpDown são desenhadas pelo Windows e ficam brancas no escuro
        foreach (var nud in new[] { due, limite }) nud.Controls[0].Visible = false;

        var cartao = new Card { Height = 238, Raio = 20, BackColor = Ui.BG };
        cartao.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 26, r.Y + 24, 38, 38), "cartao", Ui.ACC, Ui.CHIP, 12);
            Ui.Txt(g, "Cartão", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 76, r.Y + 32, 200, 22));
            Ui.Rotulo(g, "Dia do vencimento da fatura", Ui.LBL, r.X + 26, r.Y + 76);
            Ui.Fill(g, new Rectangle(r.X + 22, r.Y + 88, 132, 40), 14, Ui.FIELD);
            Ui.Borda(g, new Rectangle(r.X + 22, r.Y + 88, 132, 40), 14, Ui.LINE);
            Ui.Rotulo(g, "Limite do cartão (0 esconde)", Ui.LBL, r.X + 26, r.Y + 152);
            Ui.Fill(g, new Rectangle(r.X + 22, r.Y + 164, 172, 40), 14, Ui.FIELD);
            Ui.Borda(g, new Rectangle(r.X + 22, r.Y + 164, 172, 40), 14, Ui.LINE);
        };
        cartao.Controls.Add(due);
        cartao.Controls.Add(limite);
        due.Location = new Point(32, 100);
        limite.Location = new Point(32, 176);

        string ult = Aplicacao.UltimoBackup;
        var backup = new Card { Height = 238, Raio = 20, BackColor = Ui.BG };
        backup.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 26, r.Y + 24, 38, 38), "backup", Ui.ACC, Ui.CHIP, 12);
            Ui.Txt(g, "Backup", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 76, r.Y + 32, 240, 22));
            Ui.TxtQuebra(g, "No computador os dados abrem sozinhos, protegidos pela sua conta do Windows (DPAPI). " +
                            "O backup pede uma senha porque precisa abrir em qualquer máquina.",
                         Ui.F(12), Ui.FG3, new Rectangle(r.X + 26, r.Y + 76, r.Width - 52, 54));
            Ui.Txt(g, string.IsNullOrEmpty(ult) ? "Nenhum backup gerado ainda."
                                                : "Último backup em " + Calc.Fmt(ult),
                   Ui.F(13, true), string.IsNullOrEmpty(ult) ? Ui.WARN : Ui.OK,
                   new Rectangle(r.X + 26, r.Y + 196, r.Width - 52, 20));
        };
        var bkp = Bot("Backup criptografado", "backup", true, () => { Aplicacao.ExportarBackup(S); Render(); });
        bkp.Location = new Point(26, 140); bkp.BackColor = Ui.CARD;
        var rest = Bot("Restaurar", "exportar", false, () => {
            var novo = Aplicacao.RestaurarDeArquivo();
            if (novo == null) return;
            S = novo; Gravar();
            Aplicacao.Aviso("Backup restaurado.");
            Ir(modo, "");
        });
        rest.Location = new Point(26 + bkp.Width + 10, 140); rest.BackColor = Ui.CARD;
        backup.Controls.Add(bkp); backup.Controls.Add(rest);

        var plano = new Card { Height = 238, Raio = 20, BackColor = Ui.BG };
        plano.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 26, r.Y + 24, 38, 38), "exportar", Ui.ACC, Ui.CHIP, 12);
            Ui.Txt(g, "Exportar sem criptografia", Ui.F(17, true), Ui.FG, new Rectangle(r.X + 76, r.Y + 32, 300, 22));
            Ui.TxtQuebra(g, "Um .json legível em qualquer editor, pra levar os dados pra uma planilha. " +
                            "Sai sem senha nenhuma: trate o arquivo como documento sensível.",
                         Ui.F(12), Ui.FG3, new Rectangle(r.X + 26, r.Y + 76, r.Width - 52, 54));
        };
        var exp = Bot("Exportar .json", "exportar", false, () => Aplicacao.ExportarPlano(S));
        exp.Location = new Point(26, 140); exp.BackColor = Ui.CARD;
        plano.Controls.Add(exp);

        var perigo = new Card {
            Height = 238, Raio = 20, BackColor = Ui.BG, Fundo = Ui.PERIGO_FUNDO, Borda = Ui.RUIM_BORDA,
        };
        perigo.Desenhar = (g, r) => {
            Ui.Chip(g, new Rectangle(r.X + 26, r.Y + 24, 38, 38), "excluir", Ui.BAD, Ui.Alfa(Ui.BAD, .12), 12);
            Ui.Txt(g, "Apagar tudo", Ui.F(17, true), Ui.BAD, new Rectangle(r.X + 76, r.Y + 32, 300, 22));
            Ui.TxtQuebra(g, "Apaga pessoas, compras, dívidas e marcações de pagamento deste computador. " +
                            "Não volta, e nenhum backup é gerado automaticamente antes.",
                         Ui.F(12), Ui.FG3, new Rectangle(r.X + 26, r.Y + 76, r.Width - 52, 54));
            Ui.Txt(g, "Vai pedir a palavra APAGAR digitada.", Ui.F(12), Ui.FG3,
                   new Rectangle(r.X + 26, r.Y + 196, r.Width - 52, 18));
        };
        var apagar = Bot("Apagar tudo", "excluir", false, ApagarTudo);
        apagar.Perigo = true;
        apagar.Location = new Point(26, 140); apagar.BackColor = Ui.PERIGO_FUNDO;
        perigo.Controls.Add(apagar);

        Pilha(Grade(238, 2, new List<Control> { cartao, backup, plano, perigo }));
    }

    void ApagarTudo() {
        if (!Aplicacao.Confirma("Apagar TUDO deste computador? Isso não volta.\n\n" +
                                "Gere um backup antes se ainda não gerou.")) return;
        string r = Aplicacao.PedirTexto("Apagar tudo",
            "Pra confirmar, digite a palavra APAGAR em maiúsculas:", false);
        if (r == null) return;
        if (r.Trim() != "APAGAR") { Aplicacao.Erro("A palavra não bateu. Nada foi apagado."); return; }
        S = new Estado();
        Gravar();
        Ir("cartao", "");
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
            if (f.Excluir) { ExcluirPessoa(edit); return; }
            if (edit != null) S.people[S.people.IndexOf(edit)] = f.Resultado;
            else S.people.Add(f.Resultado);
            Gravar();
            if (aba == "" || aba == "config") Ir("cartao", "Pessoas"); else Render();
        }
    }

    void ExcluirPessoa(Pessoa p) {
        foreach (var b in S.buys.Where(x => x.pid == p.id).ToList()) {
            LimparPagos(S.paid, b.id);
            S.buys.Remove(b);
        }
        S.people.Remove(p);
        Gravar();
        Ir("cartao", "Pessoas");
    }

    void ExcluirLanc(Lanc x) {
        if (!Aplicacao.Confirma("Excluir \"" + x.name + "\" e todas as suas parcelas?")) return;
        (Cartao ? S.buys : S.debts).Remove(x);
        LimparPagos(Pagos, x.id);
        Gravar(); Render();
    }

    /// <summary>Apaga as marcações de parcela paga de um lançamento que deixou de existir.</summary>
    static void LimparPagos(Dictionary<string, string> pagos, string id) {
        foreach (var k in pagos.Keys.Where(k => k.StartsWith(id + ":")).ToList()) pagos.Remove(k);
    }
}

}
