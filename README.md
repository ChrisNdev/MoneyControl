# CardControl

Controle de **cartão compartilhado** e **dívidas pessoais**, num programa só.
Roda offline, no seu computador, com os dados **criptografados**.

Nada de servidor, nada de conta, nada de nuvem. É um `.exe` numa pasta.

---

## Instalar

1. Baixe o `CardControl.exe` da última versão em **Releases**.
2. Rode. Pronto — não tem instalador, cadastro, senha nem configuração.

Requisitos: Windows com **.NET Framework 4.x**, que já vem no Windows 8 ou mais novo.

---

## O que ele faz

**💳 Cartão compartilhado** — lança compras parceladas por pessoa, calcula quanto cada
um deve, mostra a fatura do mês, uma tabela pessoa × mês e um botão de cobrar no WhatsApp.

**🏦 Dívidas pessoais** — o que *você* deve, para quem, em quantas parcelas e quando vence,
com progresso de quitação por credor.

**Assinaturas** — em qualquer um dos dois módulos, marque `É uma assinatura` e o
lançamento se repete todo mês. Para não virar uma lista infinita, ele só gera as parcelas
**até o mês atual** — a cada mês que passa, aparece mais uma. O campo *até* encerra a
assinatura sem apagar o histórico do que já foi pago.

Para marcar uma parcela como paga, clique duas vezes nela na aba **Parcelas**.

---

## Seus dados

Ficam **só no seu computador**, em `%LOCALAPPDATA%\CardControl\dados.bin`.

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
com você logado. Ele pediria os dados ao DPAPI exatamente como o CardControl faz. Isso não
é um defeito deste programa: qualquer app que abre sozinho, sem pedir senha, tem esse
limite — a chave precisa estar ao alcance dele, logo ao alcance de quem roda como você.

Se você quer proteção **também** nesse cenário, o preço é digitar uma senha toda vez que
abrir. Aqui a escolha foi abrir sem atrito. O backup, esse sim, tem senha.

### Backup

**⚙ → Backup criptografado** gera um `.ccb` protegido por uma **senha que você escolhe na
hora** — não pela chave da máquina. Isso é de propósito: um backup que só abre neste
Windows não serviria de nada justamente no dia em que o computador morre.

| | |
|---|---|
| Derivação | PBKDF2-SHA256, 310.000 iterações, salt aleatório |
| Cifra | AES-256-CBC |
| Autenticação | HMAC-SHA256 sobre salt+IV+cifrado (encrypt-then-MAC) |

Adulterar qualquer byte do arquivo faz a abertura falhar em vez de devolver lixo. Guarde a
senha: sem ela o arquivo não abre, e não há recuperação.

Existe também *exportar sem criptografia* (`.json`), para levar os dados para uma
planilha. Esse arquivo abre em qualquer editor — trate como documento sensível.

> Apagar `%LOCALAPPDATA%\CardControl` apaga tudo. Faça backup.

---

## Compilar do código

Sem projeto, sem NuGet, sem dependência: seis arquivos e o compilador que já vem no
Windows, em `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\`.

```bat
csc /target:winexe /out:CardControl.exe ^
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

### Testes

```bat
CardControl.exe --test
```

Escreve o resultado em `testes.txt` e devolve `0` se passou, `1` se falhou. Cobre a
divisão de parcelas sem perder centavo, meses curtos e bissextos, assinaturas (fim no mês
atual, cancelamento, dia 31), totais por pessoa, o cofre inteiro (ida e volta em disco,
regravação por cima, adulteração de byte, senha errada, salt e IV novos a cada gravação) e
a montagem de todas as telas, com dados e vazias.

Os testes guardam e devolvem o `dados.bin` como estava — não mexem nos seus dados.

---

## Licença

MIT.
