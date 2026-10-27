export type FileSrcConverter = (path: string) => string;

const NOTE_IMAGE_PREFIX = "images/";

export function resolveMarkdownImageSrc(
  src: string | undefined,
  imageBaseDir: string | undefined,
  convertFileSrc: FileSrcConverter,
): string {
  if (!src) {
    return "";
  }

  if (/^https?:/i.test(src)) return ""; // External images never send diary information to a remote server.
  const normalizedSrc = src.replace(/\\/g, "/");
  if (!imageBaseDir || !normalizedSrc.startsWith(NOTE_IMAGE_PREFIX)) {
    return src;
  }

  return convertFileSrc(`${imageBaseDir}/${normalizedSrc}`);
}

