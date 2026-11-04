# Quick commands

Copy a path with a command on the end. The command runs the moment it lands on the clipboard.

```
D:\Footage\clip.mov:mp4        converts next to the original, same name
"C:\Music\take 3.wav":mp3      quotes are fine
C:\Code\my-repo:push            stages, commits with your default message and pushes
D:\Footage:mp4                  converts every compatible file in the folder
```

## The pattern

`<path>:<command>`. Everything before the last colon is the input. Everything after it is the command name.

## Built-in commands

| Command | What it does |
| --- | --- |
| `:mp4 :mov :mkv :webm :avi :gif` | Convert video (or make a GIF) |
| `:mp3 :wav :flac :m4a :ogg :opus` | Convert audio |
| `:png :jpg :webp :bmp :tiff :avif :ico` | Convert images |
| `:push` | Stage, commit and push the current branch of a repo |

Your own `:name` commands are covered in [Custom shortcuts](custom-shortcuts.md).

## Check before you run

The control centre has a command playground that explains what `path:format` will do before it runs it. Use it when you are unsure.

## Safety

- Existing files are never overwritten by conversion.
- Results appear in the Activity page, so you can see what ran and whether it worked.

## Related

- [Convert media](convert-media.md)
- [Push a repo from the clipboard](push-a-repo.md)
- [Custom shortcuts and scripts](custom-shortcuts.md)
