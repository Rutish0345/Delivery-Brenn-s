# -*- coding: utf-8 -*-
"""
Genera el kit de UI en pixel art de DeliveryBrenns.

Todo se dibuja píxel a píxel (sin antialiasing ni degradados) y se guarda en
Assets/Sprits/UI junto con su .meta de Unity (Sprite Single, PPU 16, Point,
sin compresión y Sprite Border en los que son 9-slice).

También arma la hoja de vista previa en Assets/Sprits/referencias.

Las estrellas (estrella_llena / estrella_vacia) NO se tocan: solo se leen
para mostrarlas en la vista previa.

Uso:  python Herramientas/generar_ui.py                  (todo el kit)
      python Herramientas/generar_ui.py medidor_pista ...  (solo esos sprites)
"""
import os
import re
import uuid

from PIL import Image, ImageDraw, ImageFont

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CARPETA_UI = os.path.join(RAIZ, "Assets", "Sprits", "UI")
CARPETA_REF = os.path.join(RAIZ, "Assets", "Sprits", "referencias")

# ---------------------------------------------------------------------------
# Paleta
# ---------------------------------------------------------------------------
def hexa(c):
    c = c.lstrip("#")
    return (int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), 255)

ROSA = hexa("#FF6FA5")
ROSA_BORDE = hexa("#B03A74")
ROSA_BRILLO = hexa("#FFB3D1")
ROSA_LABIO = hexa("#D9548C")      # canto inferior del botón (entre rosa y borde)
ROSA_PRESIONADO = hexa("#EE5E96")

CREMA = hexa("#FFF1D0")
CREMA_SOMBRA = hexa("#EEDAB0")
CREMA_BORDE = hexa("#C9A878")

TURQUESA = hexa("#5ED3D1")
TURQUESA_BORDE = hexa("#2C8C97")
TURQUESA_BRILLO = hexa("#A8F0EE")
TURQUESA_LABIO = hexa("#43AEB3")
TURQUESA_PRESIONADO = hexa("#52C2C2")

OSCURO = hexa("#2A1F40")
OSCURO_LUZ = hexa("#45366B")
OSCURO_SOMBRA = hexa("#1B132B")

DORADO = hexa("#FFD166")

TRANSPARENTE = (0, 0, 0, 0)


def mezclar(a, b, t):
    """Mezcla dos colores (t=0 -> a, t=1 -> b). Solo se usa para sacar tonos fijos."""
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)


# ---------------------------------------------------------------------------
# Lienzo y primitivas de dibujo (todo con bordes duros)
# ---------------------------------------------------------------------------
class Lienzo:
    def __init__(self, ancho, alto):
        self.ancho = ancho
        self.alto = alto
        self.img = Image.new("RGBA", (ancho, alto), TRANSPARENTE)

    def punto(self, x, y, color):
        if 0 <= x < self.ancho and 0 <= y < self.alto:
            self.img.putpixel((x, y), color)

    def pintar(self, pixeles, color):
        for x, y in pixeles:
            self.punto(x, y, color)

    def guardar(self, ruta):
        self.img.save(ruta)


def rect_redondeado(x0, y0, x1, y1, r):
    """Píxeles dentro de un rectángulo con esquinas de radio r (límites incluidos)."""
    pix = set()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            # se compara el centro del píxel con el círculo de la esquina más cercana
            cx = min(max(x + 0.5, x0 + r), x1 + 1 - r)
            cy = min(max(y + 0.5, y0 + r), y1 + 1 - r)
            if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                pix.add((x, y))
    return pix


def circulo(x0, y0, x1, y1):
    """Círculo (o elipse casi cuadrada) que llena la caja indicada."""
    return rect_redondeado(x0, y0, x1, y1, (x1 - x0 + 1) / 2)


def contorno(pix):
    """Píxeles del borde de una figura: los que tocan el exterior por algún lado."""
    return {(x, y) for (x, y) in pix
            if any(v not in pix for v in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))}


def desde_ascii(dibujo, x0=0, y0=0):
    """Convierte un dibujo hecho con '#' y '.' en una lista de píxeles."""
    pix = set()
    filas = [f for f in dibujo.strip("\n").split("\n")]
    for y, fila in enumerate(filas):
        for x, c in enumerate(fila.strip()):
            if c == "#":
                pix.add((x0 + x, y0 + y))
    return pix


# ---------------------------------------------------------------------------
# 1. Botón píldora rosa (9-slice)
# ---------------------------------------------------------------------------
# 24x18. Las tapas redondas caben en 8 px por lado y las filas 7-12 son
# rectas e iguales en ambos estados, así que el borde 9-slice es
# izq 8, der 8, arriba 7, abajo 5.
PILL_ANCHO, PILL_ALTO = 24, 18
PILL_BORDE = (8, 5, 8, 7)  # (izquierda, abajo, derecha, arriba) como lo pide Unity


def boton_pill(presionado):
    lz = Lienzo(PILL_ANCHO, PILL_ALTO)
    # silueta completa en tono de canto; encima el cuerpo y al final el contorno
    if not presionado:
        silueta = rect_redondeado(0, 0, 23, 17, 8)
        lz.pintar(silueta, ROSA_LABIO)
        lz.pintar(rect_redondeado(0, 0, 23, 15, 8), ROSA)
        lz.pintar(contorno(silueta), ROSA_BORDE)
        # franja de brillo arriba (uniforme en el centro para que estire bien)
        lz.pintar({(x, 2) for x in range(6, 18)}, ROSA_BRILLO)
        # destellos en la tapa izquierda y derecha
        lz.pintar({(3, 4), (2, 5), (2, 6)}, ROSA_BRILLO)
        lz.pintar({(19, 3)}, ROSA_BRILLO)
    else:
        # todo baja 2 px: se pierde el canto y el botón se ve hundido
        silueta = rect_redondeado(0, 2, 23, 17, 8)
        lz.pintar(silueta, ROSA_LABIO)
        lz.pintar(rect_redondeado(0, 2, 23, 16, 7) & silueta, ROSA_PRESIONADO)
        lz.pintar(contorno(silueta), ROSA_BORDE)
        lz.pintar({(x, 4) for x in range(6, 18)}, mezclar(ROSA_PRESIONADO, ROSA_BRILLO, 0.6))
    return lz


# ---------------------------------------------------------------------------
# 2. Botón redondo turquesa
# ---------------------------------------------------------------------------
# 20x22. El cuerpo es un círculo de 20 px y abajo lleva un canto de 2 px.
# Los íconos de 12x12 van en (4,4) sobre el normal y en (4,6) sobre el presionado.
def brillo_media_luna(caja, desplazamiento):
    """Media luna de brillo en la parte superior izquierda del círculo."""
    x0, y0, x1, y1 = caja
    cuerpo = circulo(x0, y0, x1, y1)
    corrido = circulo(x0 + desplazamiento, y0 + desplazamiento, x1 + desplazamiento, y1 + desplazamiento)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return {(x, y) for (x, y) in cuerpo - corrido if (x - cx) + (y - cy) < -4}


def boton_redondo(presionado):
    lz = Lienzo(20, 22)
    if not presionado:
        lz.pintar(circulo(0, 0, 19, 19) | circulo(0, 2, 19, 21), TURQUESA_BORDE)
        lz.pintar(circulo(1, 3, 18, 20), TURQUESA_LABIO)
        lz.pintar(circulo(1, 1, 18, 18), TURQUESA)
        lz.pintar(brillo_media_luna((1, 1, 18, 18), 2), TURQUESA_BRILLO)
        lz.pintar({(13, 4), (14, 5)}, TURQUESA_BRILLO)
    else:
        lz.pintar(circulo(0, 2, 19, 21), TURQUESA_BORDE)
        lz.pintar(circulo(1, 3, 18, 20), TURQUESA_PRESIONADO)
        lz.pintar(brillo_media_luna((1, 3, 18, 20), 1), mezclar(TURQUESA_PRESIONADO, TURQUESA_BRILLO, 0.6))
    return lz


# ---------------------------------------------------------------------------
# 3. Íconos crema de 12x12
# ---------------------------------------------------------------------------
ICONOS = {
    "icono_pausa": """
        ............
        ..###..###..
        ..###..###..
        ..###..###..
        ..###..###..
        ..###..###..
        ..###..###..
        ..###..###..
        ..###..###..
        ..###..###..
        ..###..###..
        ............
    """,
    "icono_play": """
        ............
        ...#........
        ...##.......
        ...###......
        ...####.....
        ...#####....
        ...######...
        ...#####....
        ...####.....
        ...###......
        ...##.......
        ...#........
    """,
    "icono_home": """
        ............
        .....##.....
        ....####....
        ...######...
        ..########..
        .##########.
        ..########..
        ..########..
        ..###..###..
        ..###..###..
        ..###..###..
        ............
    """,
    "icono_ajustes": """
        .....##.....
        ..#.####.#..
        .##########.
        ..###..###..
        .###....###.
        ####....####
        ####....####
        .###....###.
        ..###..###..
        .##########.
        ..#.####.#..
        .....##.....
    """,
    "icono_cerrar": """
        ............
        .##......##.
        .###....###.
        ..###..###..
        ...######...
        ....####....
        ....####....
        ...######...
        ..###..###..
        .###....###.
        .##......##.
        ............
    """,
    "icono_menu": """
        ............
        .##.#######.
        .##.#######.
        ............
        ............
        .##.#######.
        .##.#######.
        ............
        ............
        .##.#######.
        .##.#######.
        ............
    """,
    "icono_reintentar": """
        .....#......
        ...####.....
        ..######....
        .##..##.....
        .##..#....##
        ##........##
        ##........##
        ##........##
        .##......##.
        .###....###.
        ..########..
        ....####....
    """,
}


def icono(nombre):
    lz = Lienzo(12, 12)
    lz.pintar(desde_ascii(ICONOS[nombre]), CREMA)
    return lz


# ---------------------------------------------------------------------------
# 4. Marco de panel rosa con remaches (9-slice)
# ---------------------------------------------------------------------------
# 32x32 con 10 px de borde en cada lado; el centro (12x12) es crema liso.
PANEL_BORDE = (10, 10, 10, 10)


def remache(lz, x0, y0):
    lz.pintar(circulo(x0, y0, x0 + 5, y0 + 5), ROSA_BORDE)
    lz.pintar(circulo(x0 + 1, y0 + 1, x0 + 4, y0 + 4), ROSA)
    lz.punto(x0 + 2, y0 + 2, ROSA_BRILLO)


def marco_panel():
    lz = Lienzo(32, 32)
    # marco exterior
    lz.pintar(rect_redondeado(2, 2, 29, 29, 5), ROSA_BORDE)
    lz.pintar(rect_redondeado(3, 3, 28, 28, 4), ROSA)
    # brillo en el canto superior y sombra en el inferior del marco
    lz.pintar({(x, 3) for x in range(7, 25)}, ROSA_BRILLO)
    lz.pintar({(x, 28) for x in range(7, 25)}, ROSA_LABIO)
    # hueco interior crema con su línea de borde
    lz.pintar(rect_redondeado(6, 6, 25, 25, 3), ROSA_BORDE)
    lz.pintar(rect_redondeado(7, 7, 24, 24, 2), CREMA)
    # sombrita bajo el borde superior del interior
    lz.pintar({(x, 7) for x in range(9, 23)}, CREMA_SOMBRA)
    lz.pintar({(7, y) for y in range(9, 23)}, CREMA_SOMBRA)
    # remaches en las cuatro esquinas
    for x0, y0 in ((0, 0), (26, 0), (0, 26), (26, 26)):
        remache(lz, x0, y0)
    return lz


# ---------------------------------------------------------------------------
# 5. Pestaña oscura del medidor
# ---------------------------------------------------------------------------
# 32x26, lado izquierdo inclinado (1 px cada 3 filas). La carita de 18x18 va
# en (10,4) contando desde la esquina superior izquierda.
def medidor_pestana():
    ancho, alto = 32, 26
    lz = Lienzo(ancho, alto)
    for y in range(alto):
        izq = (alto - 1 - y) // 3
        for x in range(izq, ancho):
            if y == 0:
                color = OSCURO_LUZ
            elif y >= alto - 2:
                color = OSCURO_SOMBRA
            else:
                color = OSCURO
            lz.punto(x, y, color)
        # el borde inclinado lleva un píxel de luz para que se lea el corte
        if 0 < y < alto - 2:
            lz.punto(izq, y, OSCURO_LUZ)
    return lz


# ---------------------------------------------------------------------------
# 6. Barra clara del medidor
# ---------------------------------------------------------------------------
# 104x21, extremos inclinados en la misma dirección que la pestaña.
# Las 5 estrellas de 15x15 van en y=3 y x = 8, 26, 44, 62, 80 (paso de 18 px).
def medidor_barra():
    ancho, alto = 104, 21
    lz = Lienzo(ancho, alto)
    desplazamiento = (alto - 1) // 3
    for y in range(alto):
        izq = (alto - 1 - y) // 3
        der = ancho - 1 - desplazamiento + izq
        for x in range(izq, der + 1):
            borde = y == 0 or y == alto - 1 or x == izq or x == der
            if borde:
                color = CREMA_BORDE
            elif y >= alto - 3:
                color = CREMA_SOMBRA
            else:
                color = CREMA
            lz.punto(x, y, color)
    return lz


# ---------------------------------------------------------------------------
# 8. Barra del HUD (reemplaza a la pestaña y la barra de estrellas)
# ---------------------------------------------------------------------------
# Las dos piezas miden 100x12 y comparten silueta, así el relleno se pone
# encima de la pista con la misma posición y tamaño. El relleno se pinta en
# casi blanco porque en Unity se tiñe con Image.color (verde, dorado o rojo).
LAVANDA = hexa("#E2CFF7")
LAVANDA_BORDE = hexa("#F8F1FF")
LAVANDA_SOMBRA = hexa("#CDB6EC")

RELLENO = hexa("#F5F0FF")
RELLENO_BRILLO = hexa("#FFFFFF")
RELLENO_SOMBRA = hexa("#DCD2EE")
RELLENO_BORDE = hexa("#C4B7DE")

BARRA_ANCHO, BARRA_ALTO = 100, 12


def medidor_pista():
    lz = Lienzo(BARRA_ANCHO, BARRA_ALTO)
    silueta = rect_redondeado(0, 0, BARRA_ANCHO - 1, BARRA_ALTO - 1, 6)
    interior = rect_redondeado(1, 1, BARRA_ANCHO - 2, BARRA_ALTO - 2, 5)
    lz.pintar(silueta, LAVANDA_BORDE)
    lz.pintar(interior, LAVANDA)
    # sombra interior: la orilla de arriba del hueco, como si estuviera hundido
    lz.pintar({(x, y) for (x, y) in interior if (x, y - 1) not in interior}, LAVANDA_SOMBRA)
    return lz


def medidor_relleno():
    lz = Lienzo(BARRA_ANCHO, BARRA_ALTO)
    # va dentro del borde claro de la pista para que ese borde siga viéndose
    forma = rect_redondeado(1, 1, BARRA_ANCHO - 2, BARRA_ALTO - 2, 5)
    orilla = contorno(forma)
    lz.pintar(forma, RELLENO)
    lz.pintar({(x, 2) for x in range(5, BARRA_ANCHO - 5)}, RELLENO_BRILLO)
    lz.pintar({(x, y) for (x, y) in forma - orilla if (x, y + 1) in orilla}, RELLENO_SOMBRA)
    lz.pintar(orilla, RELLENO_BORDE)
    return lz


# ---------------------------------------------------------------------------
# 7. Caritas de 18x18
# ---------------------------------------------------------------------------
CARITAS = [
    # (nombre, color base, ojos/cejas/boca)
    ("carita_1", hexa("#FF5D73"), """
        ..................
        ..................
        ..................
        ..................
        ....##......##....
        .....##....##.....
        ..................
        .....##....##.....
        .....##....##.....
        ..................
        ..................
        ..................
        .......####.......
        ......#....#......
        .....#......#.....
        ..................
        ..................
        ..................
    """),
    ("carita_2", hexa("#FF9A5C"), """
        ..................
        ..................
        ..................
        ..................
        ..................
        ......#.....#.....
        .....#.......#....
        .....##....##.....
        .....##....##.....
        ..................
        ..................
        ..................
        .......####.......
        ......#....#......
        ..................
        ..................
        ..................
        ..................
    """),
    ("carita_3", hexa("#FFD166"), """
        ..................
        ..................
        ..................
        ..................
        ..................
        ..................
        .....##....##.....
        .....##....##.....
        ..................
        ..................
        ..................
        ..................
        ......######......
        ..................
        ..................
        ..................
        ..................
        ..................
    """),
    ("carita_4", hexa("#B8E06B"), """
        ..................
        ..................
        ..................
        ..................
        ..................
        ..................
        .....##....##.....
        .....##....##.....
        ..................
        ..................
        ..................
        .....#......#.....
        ......#....#......
        .......####.......
        ..................
        ..................
        ..................
        ..................
    """),
    ("carita_5", hexa("#7BD88F"), """
        ..................
        ..................
        ..................
        ..................
        ..................
        .....##....##.....
        ....#..#..#..#....
        ..................
        ..................
        ..................
        ....##########....
        .....########.....
        ......######......
        .......####.......
        ..................
        ..................
        ..................
        ..................
    """),
]


def carita(color, rasgos, feliz):
    lz = Lienzo(18, 18)
    sombra = mezclar(color, OSCURO, 0.22)
    luz = mezclar(color, CREMA, 0.6)
    cuerpo = circulo(1, 1, 16, 16)
    lz.pintar(circulo(0, 0, 17, 17), OSCURO)
    lz.pintar(cuerpo, color)
    # sombra abajo a la derecha: lo que queda fuera del círculo corrido arriba-izquierda
    lz.pintar(cuerpo - circulo(0, 0, 15, 15), sombra)
    # brillo arriba a la izquierda
    lz.pintar({(4, 3), (5, 3), (3, 4)}, luz)
    lz.pintar(desde_ascii(rasgos), OSCURO)
    if feliz:
        # lengua dentro de la boca abierta
        lz.pintar({(7, 12), (8, 12), (9, 12), (10, 12), (8, 13), (9, 13)}, ROSA)
    return lz


# ---------------------------------------------------------------------------
# .meta de Unity
# ---------------------------------------------------------------------------
PLANTILLA_PLATAFORMA = """  - serializedVersion: 4
    buildTarget: {plataforma}
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
"""

PLANTILLA_META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 16
  spriteBorder: {{x: {bx}, y: {by}, z: {bz}, w: {bw}}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
{plataformas}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def escribir_meta(ruta_png, borde=(0, 0, 0, 0)):
    """Escribe el .meta; si ya existía conserva su guid para no romper referencias."""
    ruta_meta = ruta_png + ".meta"
    guid = uuid.uuid4().hex
    if os.path.exists(ruta_meta):
        with open(ruta_meta, encoding="utf-8") as f:
            encontrado = re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M)
        if encontrado:
            guid = encontrado.group(1)
    plataformas = "".join(
        PLANTILLA_PLATAFORMA.format(plataforma=p)
        for p in ("DefaultTexturePlatform", "Standalone", "Android", "WebGL", "iOS")
    )
    izq, abajo, der, arriba = borde
    texto = PLANTILLA_META.format(
        guid=guid, bx=izq, by=abajo, bz=der, bw=arriba,
        plataformas=plataformas, sprite_id=uuid.uuid4().hex,
    )
    with open(ruta_meta, "w", encoding="utf-8", newline="\n") as f:
        f.write(texto)


# ---------------------------------------------------------------------------
# Hoja de vista previa
# ---------------------------------------------------------------------------
def hoja_vista_previa(sprites, ruta, escala=4):
    fondo = (128, 128, 128, 255)
    margen, alto_texto, columnas_max = 16, 20, 640
    fuente = ImageFont.load_default(size=13)
    medidor = ImageDraw.Draw(Image.new("RGBA", (1, 1)))

    # se acomodan en filas hasta llenar el ancho disponible
    filas, fila, ancho_fila = [], [], 0
    for nombre, img in sprites:
        ancho_celda = max(img.width * escala, int(medidor.textlength(nombre, font=fuente)) + 8)
        if fila and ancho_fila + ancho_celda > columnas_max:
            filas.append(fila)
            fila, ancho_fila = [], 0
        fila.append((nombre, img, ancho_celda))
        ancho_fila += ancho_celda + margen
    if fila:
        filas.append(fila)

    ancho_total = max(sum(c[2] + margen for c in f) for f in filas) + margen
    alto_total = margen + sum(max(c[1].height * escala for c in f) + alto_texto + margen for f in filas)
    hoja = Image.new("RGBA", (ancho_total, alto_total), fondo)
    dibujo = ImageDraw.Draw(hoja)

    y = margen
    for f in filas:
        alto_fila = max(c[1].height * escala for c in f)
        x = margen
        for nombre, img, ancho_celda in f:
            grande = img.resize((img.width * escala, img.height * escala), Image.NEAREST)
            px = x + (ancho_celda - grande.width) // 2
            py = y + (alto_fila - grande.height) // 2
            hoja.alpha_composite(grande, (px, py))
            ancho_txt = dibujo.textlength(nombre, font=fuente)
            dibujo.text((x + (ancho_celda - ancho_txt) / 2, y + alto_fila + 4), nombre,
                        fill=(20, 20, 20, 255), font=fuente)
            x += ancho_celda + margen
        y += alto_fila + alto_texto + margen
    hoja.convert("RGB").save(ruta)


def ejemplos_armados(sprites):
    """Un par de combinaciones para revisar que las piezas encajan (solo para la hoja)."""
    por_nombre = {n: lz.img for n, lz, _ in sprites}
    ejemplos = []

    # medidor del HUD: tres caritas arriba y la barra teñida de dorado al 60 %
    medidor = Image.new("RGBA", (BARRA_ANCHO, 18 + 2 + BARRA_ALTO), TRANSPARENTE)
    for i, n in enumerate(("carita_1", "carita_3", "carita_5")):
        medidor.alpha_composite(por_nombre[n], (i * 20, 0))
    medidor.alpha_composite(por_nombre["medidor_pista"], (0, 20))
    relleno = teñir(por_nombre["medidor_relleno"], DORADO)
    relleno = relleno.crop((0, 0, BARRA_ANCHO * 60 // 100, BARRA_ALTO))
    medidor.alpha_composite(relleno, (0, 20))
    ejemplos.append(("ejemplo: medidor HUD (dorado 60%)", medidor))
    for nombre_color, color in (("verde", hexa("#7BD88F")), ("rojo", hexa("#FF5D73"))):
        ejemplos.append(("ejemplo: relleno " + nombre_color, teñir(por_nombre["medidor_relleno"], color)))

    # botones redondos con ícono encima
    for estado, dy in (("normal", 4), ("presionado", 6)):
        boton = por_nombre["boton_redondo_" + estado].copy()
        boton.alpha_composite(por_nombre["icono_home"], (4, dy))
        ejemplos.append(("ejemplo: " + estado, boton))

    # píldora estirada a lo ancho con 9-slice
    ejemplos.append(("ejemplo: pill 9-slice 56px", estirar_9slice(por_nombre["boton_pill_normal"], PILL_BORDE, 56, 18)))
    ejemplos.append(("ejemplo: panel 9-slice 64x48", estirar_9slice(por_nombre["marco_panel"], PANEL_BORDE, 64, 48)))
    return ejemplos


def teñir(img, color):
    """Igual que Image.color en Unity: multiplica cada canal por el color."""
    salida = img.copy()
    pix = salida.load()
    for y in range(salida.height):
        for x in range(salida.width):
            r, g, b, a = pix[x, y]
            pix[x, y] = (r * color[0] // 255, g * color[1] // 255, b * color[2] // 255, a)
    return salida


def estirar_9slice(img, borde, ancho, alto):
    """Simula el 9-slice de Unity (modo Sliced) repitiendo las filas/columnas centrales."""
    izq, abajo, der, arriba = borde
    def mapa(v, total, ini, fin, tam):
        if v < ini:
            return v
        if v >= total - fin:
            return tam - (total - v)
        return ini + (v - ini) * (tam - ini - fin) // (total - ini - fin)
    salida = Image.new("RGBA", (ancho, alto), TRANSPARENTE)
    for y in range(alto):
        for x in range(ancho):
            sx = mapa(x, ancho, izq, der, img.width)
            sy = mapa(y, alto, arriba, abajo, img.height)
            salida.putpixel((x, y), img.getpixel((sx, sy)))
    return salida


# ---------------------------------------------------------------------------
def todos_los_sprites():
    """(nombre, lienzo, borde 9-slice) de todo el kit, en el orden de la hoja."""
    sprites = [
        ("boton_pill_normal", boton_pill(False), PILL_BORDE),
        ("boton_pill_presionado", boton_pill(True), PILL_BORDE),
        ("boton_redondo_normal", boton_redondo(False), None),
        ("boton_redondo_presionado", boton_redondo(True), None),
    ]
    sprites += [(n, icono(n), None) for n in ICONOS]
    sprites += [
        ("marco_panel", marco_panel(), PANEL_BORDE),
        ("medidor_pista", medidor_pista(), None),
        ("medidor_relleno", medidor_relleno(), None),
        ("medidor_pestana", medidor_pestana(), None),
        ("medidor_barra", medidor_barra(), None),
    ]
    for i, (nombre, color, rasgos) in enumerate(CARITAS):
        sprites.append((nombre, carita(color, rasgos, feliz=(i == 4)), None))
    return sprites


# Ya no se usan en el HUD; se dejan en disco y en la hoja, pero marcados
SIN_USO = {"medidor_pestana", "medidor_barra"}


def main(solo=None):
    """Sin argumentos genera todo el kit; con nombres, solo esos sprites.
    La hoja de vista previa siempre se rehace con todo (en memoria)."""
    os.makedirs(CARPETA_UI, exist_ok=True)
    sprites = todos_los_sprites()

    conocidos = {n for n, _, _ in sprites}
    desconocidos = set(solo or ()) - conocidos
    if desconocidos:
        raise SystemExit("No conozco estos sprites: " + ", ".join(sorted(desconocidos)))

    for nombre, lz, borde in sprites:
        if solo and nombre not in solo:
            continue
        if nombre.startswith("estrella_"):
            raise RuntimeError("Las estrellas no se regeneran")
        ruta = os.path.join(CARPETA_UI, nombre + ".png")
        lz.guardar(ruta)
        escribir_meta(ruta, borde or (0, 0, 0, 0))
        print("creado", os.path.relpath(ruta, RAIZ))

    # vista previa: el kit + las estrellas existentes (solo lectura) + ejemplos
    vista = [(n + (" (sin uso)" if n in SIN_USO else ""), lz.img) for n, lz, _ in sprites]
    for n in ("estrella_llena", "estrella_vacia"):
        ruta = os.path.join(CARPETA_UI, n + ".png")
        if os.path.exists(ruta):
            vista.append((n + " (existente)", Image.open(ruta).convert("RGBA")))
    vista += ejemplos_armados(sprites)
    ruta_hoja = os.path.join(CARPETA_REF, "hoja_vista_previa.png")
    hoja_vista_previa(vista, ruta_hoja)
    print("creado", os.path.relpath(ruta_hoja, RAIZ))


if __name__ == "__main__":
    import sys
    main(sys.argv[1:] or None)
