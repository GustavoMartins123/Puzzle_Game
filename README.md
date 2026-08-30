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

## Formatos das peças

Em `Assets/Settings/PuzzleConfig.asset`, **Cut Style** oferece cortes quadrados,
redondos, elípticos, retangulares, hexagonais, triangulares, em diamante, ondulados,
zigue-zague, com lóbulos, orgânicos, procedurais e totalmente aleatórios. Todos são
gerados matematicamente em tempo de execução; arestas vizinhas são complementares,
portanto a imagem montada continua inteira e sem distorção.
