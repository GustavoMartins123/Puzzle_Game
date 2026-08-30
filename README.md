# Puzzle Game

Unity 6000.5.10f1

## Setup

1. Clone o repositório e adicione o projeto no Unity Hub.
2. Abra `Assets/Scenes/Game.unity`.
3. Play.

## Imagens

Jogue as imagens em `Assets/Resources/PuzzleImages/`. Imagens quadradas, verticais e
horizontais são aceitas; o tabuleiro mantém automaticamente a proporção original.

Não precisa configurar nada nelas. O import e a lista do `PuzzleConfig` são ajustados
sozinhos ao adicionar, remover ou renomear um arquivo.

Se você colocou imagens na pasta antes de abrir o projeto pela primeira vez, abra
`Assets/Settings/PuzzleConfig.asset` e clique em **Rescan Folder**.

## Dificuldade e formatos das peças

`Assets/Settings/PuzzleConfig.asset` referencia três perfis explícitos: Casual, Normal e
Especialista. Cada perfil controla layouts, profundidade dos cortes, inclinação inicial,
opacidade da referência e formatos permitidos. Um perfil ou uma sessão inválida interrompe
a criação do tabuleiro com erro explícito.

Os cortes disponíveis são quadrado, redondo, elipse, retângulo, hexágono, triângulo,
diamante, ondulado, zigue-zague, lóbulos, orgânico, procedural e totalmente aleatório.
Todos são gerados matematicamente em tempo de execução; arestas vizinhas são
complementares, portanto a imagem montada continua inteira e sem distorção.

Na seleção, escolha primeiro a dificuldade e depois um formato autorizado por ela. A UI
usa opções compactas e um único preview lateral grande com a imagem real da partida. O
modo totalmente aleatório é exclusivo do perfil Especialista e mostra sua explicação em
vez de uma geometria que não representaria todas as combinações possíveis.

## Retry e retorno às opções

Ao iniciar, a partida registra uma definição imutável contendo imagem, dificuldade,
layout, formato, seed dos cortes e seed da distribuição. **Retry** reconstrói exatamente
essa mesma definição e não abre a tela de seleção. A tela de opções só reaparece ao clicar
explicitamente em **Voltar às opções**, preservando a dificuldade e o formato atuais para
edição.
