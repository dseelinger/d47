# AGENTS.md

A voice companion for a space game. It reads the game's journal files and answers out loud.

## The layering rule

- `Core` depends on nothing but logging abstractions.
- Each spoke (`Audio`, `Tts`, `Stt`) references only `Core`. No spoke references another.
- `App` references every project and is the only place they meet.

## The tick discipline

A tick is synchronous and must not block. Awaitable work goes to the thread pool through a queue.

## The source

The source tree is not in this folder. Plan from this file and the issue; do not search for code.
