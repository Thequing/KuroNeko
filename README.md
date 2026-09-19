# KuroNeko

A visual novel built solo in Unity, driven by a narrative script language I wrote:
plain text in, branching scene out.

The interesting part isn't the novel — it's the parser. Writers shouldn't have to open
the editor to add a scene, and they shouldn't find out a jump is broken by walking into
it during play.

---

## The script language

Ten commands. Parameters are `key=value`, anywhere in the line, quoted when they contain
spaces. Lines beginning with `#` are comments; bare lines are dialogue.

| Command | What it does |
|---|---|
| `@scene` | loads a scene by name |
| `@show` / `@hide` | a `bg`, `sprite`, `char` or `textbox`, by `name` |
| `@play` / `@stop` | `bgm` or `sfx` |
| `@set` | assigns a variable |
| `@flag` | `add=` or `remove=` a story flag |
| `@if` | `key=` `value=` `goto=<label>` — conditional jump |
| `@jump` | `to=<label>` — unconditional jump |
| `@menu` | a choice; options follow on the next lines, each starting with `*` or `-` |

```
# chapter one
@scene name=alley
@show target=bg name=alley_night
@play type=bgm name=rain

Kuro: You came back.

@menu
* Follow the cat -> label_forest
- Turn around    -> label_home
```

## It validates the whole script before a word is shown

`Parser.cs` runs a diagnostic pass over the entire file at load and reports every problem
at once, with line numbers, instead of failing at the moment a player reaches it. It
catches:

- unrecognised commands
- a command missing a required parameter — `@jump` with no `to=`, `@show` with no `name=`
- a `target=` or `type=` that isn't one of the accepted values
- `@jump` and `@if goto=` pointing at labels that don't exist
- duplicate labels
- `@menu` with no choices under it

Errors and warnings are separated, so a `@set` with no parameters warns (it does nothing)
while a dead jump is an error (it breaks the story).

## Structure

The parser has **no Unity dependency** — it turns text into nodes and diagnostics and
nothing else. `VisualNovelEngine.cs` sits above it and is the only part that knows about
scenes, sprites, audio and the textbox. `SaveSlotManager` / `SaveData` handle multiple
save slots, and `ResourceManager` resolves assets by name.

```
Assets/Resources/
  Scripts/
    Parser.cs              the language: tokenise, parse, diagnose
    VisualNovelEngine.cs   executes parsed nodes against Unity
    GameManager.cs         flow, flags, variables
    SaveData.cs / SaveSlotManager.cs / SaveSlotUI.cs
    MainMenu.cs / PauseMenu.cs / SettingsManager.cs
    Menu/                  CurvedText, OcasionalBlink
  ScenesTXT/               the scripts themselves
```

**Engine:** Unity · C# · TextMesh Pro
**Fonts:** Jersey 15 (SIL OFL)

---

<details>
<summary><b>Português</b></summary>

<br>

Visual novel feita sozinho em Unity, movida por uma linguagem de script narrativo que
escrevi: texto puro entra, cena com ramificação sai.

A parte interessante não é a novel — é o parser. Quem escreve não deveria precisar abrir
o editor para adicionar uma cena, nem descobrir que um salto está quebrado ao esbarrar
nele durante o jogo.

**Dez comandos:** `@scene`, `@show`, `@hide`, `@play`, `@stop`, `@set`, `@flag`, `@if`,
`@jump`, `@menu`. Parâmetros são `chave=valor`, em qualquer ponto da linha. `#` comenta;
linha solta é diálogo.

**Um passo de diagnóstico** percorre o script inteiro no carregamento e reporta tudo de
uma vez, com número de linha: comando desconhecido, parâmetro obrigatório ausente,
`target=`/`type=` inválido, salto para label inexistente, label duplicado e `@menu` sem
escolhas. Erro e aviso são separados — `@set` vazio avisa, salto morto é erro.

O parser **não depende do Unity**. `VisualNovelEngine.cs` é a única camada que conhece
cena, sprite, áudio e caixa de texto.

</details>
