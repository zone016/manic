# Manic

Uma biblioteca para fuçar nos processos alheios de forma confiável, performática e ~~quase~~ totalmente gerenciável.

- **Sem brigas com UAC** (talvez com a ALU :funny:);
- **Instanciou, abraço**. Padrões de código FTW;
- Se você já usa `memory.dll` ou similares, **vai se adaptar fácil**; e
- **API bem intuitiva**, e ~~quase~~ documentada!

> **Nota**: Ainda não há uma *release* pronta para *trainers* ou aplicações produtivas, por tanto não disponibilizamos um pacote ainda.

# *Memory Pattern Scanning*

Assim como qualquer biblioteca que é feita para brincar com jogos, ter uma implementação legal de um "AoB" é essencial, aqui você pode importar seu *pattern* diretamente do IDA ou do Cheat Engine:

```csharp
using var manic = new Manic(gameProcess.Id);

var healthAddress = manic.BinaryPatternSearch("48xxxxxxxxxxxxxxxxxx49");

var playerPointer = manic.BinaryPatternSearch(new byte[]{
    0x4C, 0x89, 0x3D, 0x00, 0x00, 0x00, 0x00, 0x48
}, gameProcess.MainModule.BaseAddress).ToArray();

var playerNamePointer = manic.BinaryPatternSearch("41 FF D1 ?? 45 50");
```

Da mesma forma funciona para funções de RW nos endereços ou ponteiros, sem crise, e bem fácil de fazer funcionar.

# Operações na Memória

Seguindo a linha, também não tem mais complicações em importar a assinatura errada para `VirtualAllocEx` ou `ReadProcessMemory`:

```csharp
var player = results[0];

player = IntPtr.Add(player, 0x02);
player = manic.ReadVirtualMemory<IntPtr>(player);
player = IntPtr.Subtract(player, 0x08);
player = manic.ReadVirtualMemory<IntPtr>(player);
```

Caso fosse no Cheat Engine, o código acima seria `[player+0x2]-8`, também tenho nos planos de fazer esse tipo de sintaxe no Mimic. Por hora, também podemos ler e escrever na memória virtual de forma bem fácil:

```csharp
var stamina = manic.ReadVirtualMemory<float>(staminaAddress);

if (stamina <= 15)
{
    var maxStamina = manic.ReadVirtualMemory<float>(maxStaminaAddress);
    manic.WriteVirtualMemory(staminaAddress, maxStamina);    
}
```

Agora o *player* nunca mais fica cansado durante aquela gameplay mais intensa.

# Exemplo Prático

Dando uma caçada na *interwebs*, encontrei a Cheat Table do [Akira](https://fearlessrevolution.com/memberlist.php?mode=viewprofile&u=29812) no FearLess, e implementei o *pointer scanning* da entidade `Player` do Valheim [aqui](https://github.com/zone016/manic/blob/main/Manic.Example/Program.cs), então basta você fazer o *clone* do repositório, e vai ter um *trainer* bem simples, mas funcional para o game, saca só:

![O `Manic.Example` rodando.](https://raw.githubusercontent.com/zone016/assets/main/211e0cc8-a225-4d4b-9fcd-80791babce77.gif)

A parte mais massa? Nem *token* administrativo, para acessar essas APIs não há necessidade de ser administrador, a não ser que o processo que esteja de olho fique fora das suas ACLs.

> **Note**: Want to see this `README.md` in English as well? Submit your PR!
