<h1 align="center">MoneyControl</h1>

<p align="center">
  Cartão compartilhado e dívidas pessoais, num programa só.<br>
  Roda offline, no seu computador, com os dados criptografados.
</p>

<p align="center">
  <img alt="Windows" src="https://img.shields.io/badge/Windows-8%2B-0078D6">
  <img alt=".NET Framework" src="https://img.shields.io/badge/.NET%20Framework-4.x-512BD4">
  <img alt="Dependências" src="https://img.shields.io/badge/depend%C3%AAncias-nenhuma-success">
  <img alt="Licença" src="https://img.shields.io/badge/licen%C3%A7a-MIT-blue">
</p>

<p align="center">
  <img src="docs/cartao.png" alt="Resumo do cartão compartilhado" width="880">
</p>

---

## Baixar

1. Baixe o **`MoneyControl.exe`** em [Releases](../../releases).
2. Rode.

É isso. Um arquivo, sem instalador, sem cadastro, sem senha, sem configuração inicial.
Nada de servidor, nada de conta, nada de nuvem.

Precisa do .NET Framework 4.x, que já vem no Windows 8 ou mais novo.

---

## O que ele faz

<table>
<tr>
<td width="50%" valign="top">

### 💳 Cartão compartilhado

Você passa o cartão, várias pessoas usam. O programa lança as compras por pessoa,
divide as parcelas, mostra a fatura do mês e diz quanto cada um te deve — com um
botão que já abre a cobrança pronta no WhatsApp.

</td>
<td width="50%" valign="top">

### 🏦 Dívidas pessoais

O outro lado: o que **você** deve, para quem, em quantas parcelas e quando vence.
Progresso de quitação por credor, parcelas atrasadas em vermelho, e o total que
falta para ficar livre.

</td>
</tr>
</table>

Os dois módulos vivem no mesmo programa e no mesmo backup.

<p align="center">
  <img src="docs/hub.png" alt="Tela inicial com os dois módulos" width="880">
</p>

### Parcelas que fecham no centavo

Dividir R$ 100 em 3 não dá 33,33 três vezes. A última parcela absorve a sobra, então a
soma sempre bate com o total. `31/01 + 1 mês` vira `28/02`, e `29/02` em ano bissexto.

### Assinaturas que não viram lista infinita

Marque `É uma assinatura` e o lançamento se repete todo mês, sem fim. Para não gerar
parcelas até o fim dos tempos, ele só cria **até o mês atual** — a cada mês que passa,
aparece mais uma. O campo *até* encerra a assinatura sem apagar o histórico do que já
foi pago.

### Uma tabela pessoa × mês

Quanto ainda falta em cada mês de vencimento, para cada pessoa. `✓` é mês quitado.

<p align="center">
  <img src="docs/pormes.png" alt="Tabela de pessoa por mês" width="880">
</p>

### Parcelas agrupadas por mês

Clique duplo na linha marca ou desmarca como paga.

<p align="center">
  <img src="docs/parcelas.png" alt="Lista de parcelas agrupadas por mês" width="880">
</p>

---

## Seus dados

Ficam **só no seu computador**, num arquivo em `%LOCALAPPDATA%\MoneyControl\dados.bin`.

| | |
|---|---|
| No disco | um arquivo só, cifrado inteiro; nenhum campo em texto legível |
| Proteção | DPAPI do Windows, escopo `CurrentUser` |
| Chave | o Windows guarda, derivada das credenciais da sua conta |
| Gravação | escreve num temporário e troca pelo bom — queda de energia não deixa você sem nada |

Não existe arquivo de chave para alguém copiar: quem protege é o Windows.

### O que isso protege — e o que não

**Protege:** quem copiar o `dados.bin` para outro computador não abre nada. Outro usuário
do mesmo Windows também não. Seu backup roubado não abre. Ninguém lê seus dados olhando
os arquivos.

**Não protege** contra um programa malicioso rodando **na sua própria conta do Windows**,
com você logado. Ele pediria os dados ao DPAPI exatamente como o MoneyControl faz. Isso não
é um defeito deste programa: qualquer app que abre sozinho, sem pedir senha, tem esse
limite — a chave precisa estar ao alcance dele, logo ao alcance de quem roda como você.

Se você quer proteção **também** nesse cenário, o preço é digitar uma senha toda vez que
abrir. Aqui a escolha foi abrir sem atrito. O backup, esse sim, tem senha.

### Backup

**Config → Backup criptografado** gera um `.mcb` protegido por uma **senha que você
escolhe na hora** — não pela chave da máquina. Isso é de propósito: um backup que só abre
neste Windows não serviria de nada justamente no dia em que o computador morre.

| | |
|---|---|
| Derivação | PBKDF2-SHA256, 310.000 iterações, salt aleatório |
| Cifra | AES-256-CBC |
| Autenticação | HMAC-SHA256 sobre salt+IV+cifrado (encrypt-then-MAC) |

Adulterar qualquer byte do arquivo faz a abertura falhar em vez de devolver lixo. Guarde a
senha: sem ela o arquivo não abre, e não há recuperação.

Existe também *exportar sem criptografia* (`.json`), para levar os dados para uma
planilha. Esse arquivo abre em qualquer editor — trate como documento sensível.

> Apagar `%LOCALAPPDATA%\MoneyControl` apaga tudo. Faça backup.

---

## Compilar do código

Sem projeto, sem NuGet, sem dependência: seis arquivos e o compilador que já vem no
Windows, em `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`.

```bat
csc /target:winexe /out:MoneyControl.exe ^
    /reference:System.dll /reference:System.Core.dll ^
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll ^
    /reference:System.Security.dll /reference:System.Web.Extensions.dll ^
    src\*.cs
```

| | |
|---|---|
| `src\Modelo.cs` | dados e cálculo — parcelas, assinaturas, totais. Sem UI. |
| `src\Cofre.cs` | gravação cifrada e backup com senha |
| `src\Janela.cs` | janela principal: hub, abas, tabelas |
| `src\Dialogos.cs` | cadastro de lançamento, pessoa e configurações |
| `src\Aplicacao.cs` | entrada, paleta e as peças de UI reaproveitadas |
| `src\Testes.cs` | a bateria de verificação |

Compra do cartão e dívida pessoal usam **o mesmo formulário e o mesmo gerador de
parcelas** — a forma das duas é idêntica, e duas cópias seriam dois lugares para errar.

### Testes

```bat
MoneyControl.exe --test
```

Escreve o resultado em `testes.txt` e devolve `0` se passou, `1` se falhou. Cobre a
divisão de parcelas sem perder centavo, meses curtos e bissextos, assinaturas (fim no mês
atual, cancelamento, dia 31), totais por pessoa, o cofre inteiro (ida e volta em disco,
regravação por cima, adulteração de byte, senha errada, salt e IV novos a cada gravação) e
a montagem de todas as telas e caixas de cadastro, com dados e vazias.

Os testes guardam e devolvem o `dados.bin` como estava — não mexem nos seus dados.

---

## Licença

MIT.
