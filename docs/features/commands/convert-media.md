# Convert media

Copy a file path, add `:format` on the end, and the converted file appears next to the original with the same name.

![The Conversion page](../../screenshots/control-conversion.png)

## Convert a video

1. Copy the file's path, for example `D:\Footage\clip.mov`.
2. Make the copied text end in `:mp4`, for example `D:\Footage\clip.mov:mp4`, and copy that.
3. A toast reports progress and then the result.

You can also press a format chip on the toast of a copied media file; that does the same in one click.

## Formats

The Conversion page lists every format with a search box and Video, Audio and Image filters. Each row shows
the `:command`, the file extension, when to pick it, and what it can be made from. **Convert files…** on a row
opens a picker limited to files that convert to that format; one file runs as a single conversion, several run
as a batch under one toast.

| Kind | Commands |
| --- | --- |
| Video | `mp4` (H.264), `hevc` (H.265 in MP4), `av1` (AV1 in MP4), `webm`, `mkv` (stream copy), `mov`, `m4v`, `avi`, `wmv`, `flv`, `mpg` (MPEG-2), `ts`, `3gp` |
| Animation | `gif`, `apng` (made from a video or an image) |
| Audio | `mp3`, `m4a`, `aac`, `opus`, `ogg`, `flac`, `alac`, `wav`, `aiff`, `wma`, `ac3`, `mka` (audio copy) |
| Images | `png`, `jpg` (or `jpeg`), `webp`, `avif`, `jxl`, `bmp`, `tiff`, `tga`, `qoi`, `ppm`, `jp2`, `ico` |

Files the app recognises as a source also grew: video now includes `.vob .mxf .f4v .m2v .asf .divx .rm .rmvb
.y4m .hevc .h264 .h265 .3g2`; audio includes `.ac3 .eac3 .mka .mp2 .caf .dts .wv .ape .au .oga .m4b .tta .spx`;
images include `.jxl .tga .qoi .ppm .pgm .pbm .pcx .dds .exr .hdr .jp2 .j2k .psd .sgi .apng .jfif`.

A folder batch to a still image format takes the images in the folder only; `gif` and `apng` also take videos.

## A whole folder

`D:\Footage:mp4` converts every compatible file in the folder.

## FFmpeg

Conversion uses FFmpeg. FFmpeg, FFprobe and portable Git are bundled with the installer, so it works offline. If FFmpeg is missing, the toast offers to install it with winget, or you can point to your own copy on the Conversion page.

## Failure modes

- A file that already exists with the target name is left alone; nothing is overwritten.
- If FFmpeg reports an error, the Activity page keeps its message.

## Related

- [Quick commands](quick-commands.md)
- [Clipboard toasts](../clipboard/toasts.md)
