// Caixas de cadastro. Compra do cartão e dívida pessoal usam o mesmo formulário:
// muda o rótulo de "quem" e pouco mais — duas telas iguais seriam dois lugares pra errar.
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using A = MoneyControl.Aplicacao;

namespace MoneyControl {

public class FormLanc : Form {
    public Lanc Resultado;

    readonly bool cartao;
    readonly Estado S;
    readonly Lanc original;

    readonly CheckBox rec = new CheckBox();
    readonly ComboBox quem = new ComboBox();
    readonly TextBox nome = new TextBox();
    readonly NumericUpDown total = new NumericUpDown();
    readonly NumericUpDown n = new NumericUpDown();
    readonly DateTimePicker ate = new DateTimePicker();
    readonly DateTimePicker data = new DateTimePicker();
    readonly DateTimePicker first = new DateTimePicker();
    readonly ComboBox cat = new ComboBox();
    readonly Label previa = new Label();
    readonly Label lblTotal = new Label(), lblN = new Label(), lblAte = new Label(), lblFirst = new Label();

    public FormLanc(Estado s, bool cartao, Lanc edit) {
        S = s; this.cartao = cartao; original = edit;

        Text = (edit != null ? "Editar " : "Nova ") + (cartao ? "compra" : "dívida");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 430);
        BackColor = A.BG; ForeColor = A.FG; Font = new Font("Segoe UI", 9f);

        rec.Text = "É uma assinatura — repete todo mês, sem fim";
        rec.ForeColor = A.FG;
        rec.CheckedChanged += (o, e) => Sincronizar();

        quem.DropDownStyle = cartao ? ComboBoxStyle.DropDownList : ComboBoxStyle.DropDown;
        if (cartao) {
            foreach (var p in S.people) quem.Items.Add(p.nome);
        } else {
            // credor não tem cadastro, só nome: os já usados viram sugestão
            quem.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            quem.AutoCompleteSource = AutoCompleteSource.CustomSource;
            var fonte = new AutoCompleteStringCollection();
            foreach (var c in Calc.ColunasDividas(S)) { quem.Items.Add(c.nome); fonte.Add(c.nome); }
            quem.AutoCompleteCustomSource = fonte;
        }

        total.DecimalPlaces = 2; total.Maximum = 9999999; total.Increment = 10;
        n.Minimum = 1; n.Maximum = cartao ? 72 : 360; n.Value = 1;

        ate.Format = DateTimePickerFormat.Custom;
        ate.CustomFormat = "MMM'/'yyyy";
        ate.ShowCheckBox = true;      // desmarcado = sem fim, que é o padrão de uma assinatura
        ate.Checked = false;

        data.Format = first.Format = DateTimePickerFormat.Short;
        data.ValueChanged += (o, e) => {
            if (original != null) return;                 // editando, não mexe no que o usuário já definiu
            first.Value = Calc.Data(cartao
                ? Calc.Venc1(Ymd(data), S.cfg.due)
                : Calc.AddMeses(Ymd(data), 1));
        };

        cat.DropDownStyle = ComboBoxStyle.DropDownList;
        cat.Items.AddRange(Calc.CATS);

        total.ValueChanged += (o, e) => Previa();
        n.ValueChanged += (o, e) => Previa();
        ate.ValueChanged += (o, e) => Previa();

        previa.ForeColor = A.MUTED;

        Montar();
        Preencher(edit);
        Sincronizar();
    }

    string Ymd(DateTimePicker d) { return d.Value.ToString("yyyy-MM-dd"); }

    void Montar() {
        int y = 12;
        Action<Control, int, int, int> por = (c, x, w, alt) => {
            c.Location = new Point(x, y); c.Width = w; if (alt > 0) c.Height = alt;
            Controls.Add(c);
        };
        Func<string, int, int, Label> rot = (txt, x, w) => {
            var l = new Label { Text = txt, Location = new Point(x, y), Width = w, Height = 16,
                                ForeColor = A.MUTED, Font = new Font("Segoe UI", 7.5f) };
            Controls.Add(l);
            return l;
        };

        por(rec, 14, 442, 24); y += 34;

        rot(cartao ? "Pessoa" : "Para quem eu devo", 14, 220);
        rot("O que foi", 248, 208); y += 18;
        por(quem, 14, 220, 0); quem.Location = new Point(14, y);
        nome.Location = new Point(248, y); nome.Width = 208; Controls.Add(nome);
        y += 30;

        lblTotal.Location = new Point(14, y); lblTotal.Width = 220; lblTotal.Height = 16;
        lblTotal.ForeColor = A.MUTED; lblTotal.Font = new Font("Segoe UI", 7.5f); Controls.Add(lblTotal);
        lblN.Location = new Point(248, y); lblN.Width = 208; lblN.Height = 16;
        lblN.ForeColor = A.MUTED; lblN.Font = new Font("Segoe UI", 7.5f); Controls.Add(lblN);
        lblAte.Location = new Point(248, y); lblAte.Width = 208; lblAte.Height = 16;
        lblAte.ForeColor = A.MUTED; lblAte.Font = new Font("Segoe UI", 7.5f); Controls.Add(lblAte);
        y += 18;
        total.Location = new Point(14, y); total.Width = 220; Controls.Add(total);
        n.Location = new Point(248, y); n.Width = 208; Controls.Add(n);
        ate.Location = new Point(248, y); ate.Width = 208; Controls.Add(ate);
        y += 32;

        rot(cartao ? "Data da compra" : "Quando assumi", 14, 220);
        lblFirst.Location = new Point(248, y); lblFirst.Width = 208; lblFirst.Height = 16;
        lblFirst.ForeColor = A.MUTED; lblFirst.Font = new Font("Segoe UI", 7.5f); Controls.Add(lblFirst);
        y += 18;
        data.Location = new Point(14, y); data.Width = 220; Controls.Add(data);
        first.Location = new Point(248, y); first.Width = 208; Controls.Add(first);
        y += 32;

        rot("Categoria", 14, 442); y += 18;
        cat.Location = new Point(14, y); cat.Width = 442; Controls.Add(cat);
        y += 34;

        previa.Location = new Point(14, y); previa.Size = new Size(442, 34); Controls.Add(previa);

        var ok = A.Btn("Salvar", (o, e) => Salvar(), true);
        ok.Location = new Point(356, 386); ok.Width = 100;
        var cancel = A.Btn("Cancelar", null);
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Location = new Point(248, 386); cancel.Width = 100;
        Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;

        foreach (Control c in Controls) {
            if (c is TextBox || c is ComboBox || c is NumericUpDown) {
                c.BackColor = A.SURF2; c.ForeColor = A.FG;
            }
        }
    }

    void Preencher(Lanc b) {
        if (b == null) {
            if (quem.Items.Count > 0 && cartao) quem.SelectedIndex = 0;
            data.Value = DateTime.Today;
            first.Value = Calc.Data(cartao ? Calc.Venc1(Calc.Hoje(), S.cfg.due) : Calc.AddMeses(Calc.Hoje(), 1));
            cat.SelectedItem = "Outros";
            return;
        }
        if (cartao) {
            // pela posição, não pelo nome: duas pessoas podem se chamar igual
            int i = S.people.FindIndex(x => x.id == b.pid);
            quem.SelectedIndex = i >= 0 ? i : (S.people.Count > 0 ? 0 : -1);
        } else {
            quem.Text = b.pid;
        }
        nome.Text = b.name;
        total.Value = Math.Min(total.Maximum, (decimal)b.total);
        n.Value = Math.Min(n.Maximum, Math.Max(1, b.n));
        data.Value = Calc.Data(b.date);
        first.Value = Calc.Data(b.first);
        cat.SelectedItem = Calc.CATS.Contains(b.cat) ? b.cat : "Outros";
        rec.Checked = b.rec;
        if (!string.IsNullOrEmpty(b.ate)) { ate.Checked = true; ate.Value = Calc.Data(b.ate + "-01"); }
    }

    /// <summary>Assinatura não tem número de parcelas — o campo vira "cobrar até".</summary>
    void Sincronizar() {
        bool r = rec.Checked;
        n.Visible = lblN.Visible = !r;
        ate.Visible = lblAte.Visible = r;
        lblTotal.Text = r ? "Valor por mês (R$)" : "Valor total (R$)";
        lblN.Text = "Parcelas";
        lblAte.Text = cartao ? "Cobrar até (opcional)" : "Pagar até (opcional)";
        lblFirst.Text = r ? "1ª cobrança" : "1ª parcela vence";
        Previa();
    }

    void Previa() {
        double t = (double)total.Value;
        if (t <= 0) { previa.Text = ""; return; }
        if (rec.Checked) {
            previa.Text = Calc.Brl(t) + " por mês, todo mês" +
                (ate.Checked ? " até " + Calc.MesLabel(ate.Value.ToString("yyyy-MM")) : " (sem fim)");
            return;
        }
        int q = (int)n.Value;
        var v = Calc.Dividir(t, q);
        previa.Text = q + "x de " + Calc.Brl(v[0]) +
            (v[q - 1] != v[0] ? " (última " + Calc.Brl(v[q - 1]) + ")" : "");
    }

    void Salvar() {
        string dono = cartao
            ? (quem.SelectedIndex >= 0 ? S.people[quem.SelectedIndex].id : "")
            : quem.Text.Trim();
        if (string.IsNullOrEmpty(dono)) { A.Erro(cartao ? "Escolha a pessoa." : "Diga para quem você deve."); return; }
        if (string.IsNullOrEmpty(nome.Text.Trim())) { A.Erro("Falta dizer o que é."); return; }
        if (total.Value <= 0) { A.Erro("O valor precisa ser maior que zero."); return; }

        bool r = rec.Checked;
        Resultado = new Lanc {
            id = original != null ? original.id : Calc.NovoId(),
            pid = dono, name = nome.Text.Trim(), total = (double)total.Value,
            n = r ? 1 : (int)n.Value, date = Ymd(data), first = Ymd(first),
            cat = (string)cat.SelectedItem ?? "Outros",
            rec = r, ate = r && ate.Checked ? ate.Value.ToString("yyyy-MM") : "",
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}

/* ============================== pessoa ============================== */

public class FormPessoa : Form {
    public Pessoa Resultado;
    string cor;

    public FormPessoa(Estado s, Pessoa edit) {
        Text = edit != null ? "Editar pessoa" : "Nova pessoa";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 210);
        BackColor = A.BG; ForeColor = A.FG; Font = new Font("Segoe UI", 9f);

        cor = edit != null ? edit.cor : Calc.CORES[s.people.Count % Calc.CORES.Length];

        var nome = new TextBox { Location = new Point(14, 32), Width = 200, BackColor = A.SURF2, ForeColor = A.FG };
        var fone = new TextBox { Location = new Point(224, 32), Width = 182, BackColor = A.SURF2, ForeColor = A.FG };
        if (edit != null) { nome.Text = edit.nome; fone.Text = edit.fone; }

        Controls.Add(new Label { Text = "Nome", Location = new Point(14, 14), Width = 200, Height = 16,
                                 ForeColor = A.MUTED, Font = new Font("Segoe UI", 7.5f) });
        Controls.Add(new Label { Text = "WhatsApp (DDD+número)", Location = new Point(224, 14), Width = 182,
                                 Height = 16, ForeColor = A.MUTED, Font = new Font("Segoe UI", 7.5f) });
        Controls.Add(nome); Controls.Add(fone);

        var paleta = new FlowLayoutPanel { Location = new Point(14, 76), Size = new Size(392, 40) };
        foreach (var c in Calc.CORES) {
            string dessa = c;
            var b = new Button {
                Width = 34, Height = 30, FlatStyle = FlatStyle.Flat,
                BackColor = ColorTranslator.FromHtml(dessa), Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand, Text = "",
            };
            b.FlatAppearance.BorderSize = dessa == cor ? 2 : 0;
            b.FlatAppearance.BorderColor = A.FG;
            b.Click += (o, e) => {
                cor = dessa;
                foreach (Control x in paleta.Controls)
                    ((Button)x).FlatAppearance.BorderSize =
                        ((Button)x).BackColor == ColorTranslator.FromHtml(dessa) ? 2 : 0;
            };
            paleta.Controls.Add(b);
        }
        Controls.Add(new Label { Text = "Cor", Location = new Point(14, 58), Width = 200, Height = 16,
                                 ForeColor = A.MUTED, Font = new Font("Segoe UI", 7.5f) });
        Controls.Add(paleta);

        var ok = A.Btn("Salvar", (o, e) => {
            if (string.IsNullOrEmpty(nome.Text.Trim())) { A.Erro("Falta o nome."); return; }
            Resultado = new Pessoa {
                id = edit != null ? edit.id : Calc.NovoId(),
                nome = nome.Text.Trim(), fone = fone.Text.Trim(), cor = cor,
            };
            DialogResult = DialogResult.OK;
            Close();
        }, true);
        ok.Location = new Point(306, 160); ok.Width = 100;
        var cancel = A.Btn("Cancelar", null);
        cancel.DialogResult = DialogResult.Cancel;
        cancel.Location = new Point(198, 160); cancel.Width = 100;
        Controls.Add(ok); Controls.Add(cancel);
        AcceptButton = ok; CancelButton = cancel;
    }
}

/* ============================== configuração ============================== */

public class FormCfg : Form {
    public Estado Estado;          // pode ser trocado por uma restauração de backup
    public bool Mudou;

    public FormCfg(Estado s) {
        Estado = s;
        Text = "Configurações";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 320);
        BackColor = A.BG; ForeColor = A.FG; Font = new Font("Segoe UI", 9f);

        var due = new NumericUpDown { Location = new Point(14, 32), Width = 220, Minimum = 1, Maximum = 28,
                                      Value = s.cfg.due, BackColor = A.SURF2, ForeColor = A.FG };
        var limite = new NumericUpDown { Location = new Point(248, 32), Width = 208, Maximum = 9999999,
                                         DecimalPlaces = 2, Increment = 100, Value = (decimal)s.cfg.limit,
                                         BackColor = A.SURF2, ForeColor = A.FG };
        Controls.Add(new Label { Text = "Dia do vencimento da fatura", Location = new Point(14, 14), Width = 220,
                                 Height = 16, ForeColor = A.MUTED, Font = new Font("Segoe UI", 7.5f) });
        Controls.Add(new Label { Text = "Limite do cartão (0 = ocultar)", Location = new Point(248, 14), Width = 208,
                                 Height = 16, ForeColor = A.MUTED, Font = new Font("Segoe UI", 7.5f) });
        Controls.Add(due); Controls.Add(limite);

        var bkp = A.Btn("Backup criptografado", (o, e) => Aplicacao.ExportarBackup(Estado));
        bkp.Location = new Point(14, 76);
        var rest = A.Btn("Restaurar backup", (o, e) => {
            var novo = Aplicacao.RestaurarDeArquivo();
            if (novo == null) return;
            Estado = novo; Mudou = true;
            due.Value = novo.cfg.due; limite.Value = (decimal)novo.cfg.limit;
            A.Aviso("Backup restaurado.");
        });
        rest.Location = new Point(14 + bkp.Width + 8, 76);
        Controls.Add(bkp); Controls.Add(rest);

        Controls.Add(new Label {
            Text = "No computador os dados abrem sozinhos, protegidos pela sua conta do Windows (DPAPI).\n" +
                   "O backup pede uma senha porque precisa abrir em qualquer máquina.",
            Location = new Point(14, 114), Size = new Size(442, 36), ForeColor = A.MUTED,
            Font = new Font("Segoe UI", 7.5f),
        });

        var plano = A.Btn("exportar sem criptografia", (o, e) => Aplicacao.ExportarPlano(Estado));
        plano.Location = new Point(14, 158);
        Controls.Add(plano);
        Controls.Add(new Label {
            Text = "Sem criptografia o arquivo abre em qualquer editor — use só pra levar os dados pra outro lugar.",
            Location = new Point(14, 192), Size = new Size(442, 30), ForeColor = A.MUTED,
            Font = new Font("Segoe UI", 7.5f),
        });

        var apagar = A.Btn("Apagar tudo", (o, e) => {
            if (!A.Confirma("Apagar TUDO? Isso não volta.")) return;
            if (!A.Confirma("Tem certeza mesmo? Exporte um backup antes se ainda não exportou.")) return;
            Estado = new Estado(); Mudou = true;
            due.Value = Estado.cfg.due; limite.Value = 0;
        });
        apagar.Location = new Point(14, 240);
        apagar.ForeColor = A.BAD;
        Controls.Add(apagar);

        var fechar = A.Btn("Fechar", (o, e) => {
            Estado.cfg.due = (int)due.Value;
            Estado.cfg.limit = (double)limite.Value;
            Mudou = true;
            DialogResult = DialogResult.OK;
            Close();
        }, true);
        fechar.Location = new Point(356, 240); fechar.Width = 100;
        Controls.Add(fechar);
        AcceptButton = fechar;

        Controls.Add(new Label { Text = "MoneyControl v" + Aplicacao.VERSAO, Location = new Point(14, 284),
                                 Width = 442, Height = 16, ForeColor = A.MUTED,
                                 Font = new Font("Segoe UI", 7.5f), TextAlign = ContentAlignment.MiddleRight });
    }
}

}
