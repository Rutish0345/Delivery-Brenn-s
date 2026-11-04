"""Arte del PanelMenu de MenuPrincipal sobre "El mirador" (octubre 2026): botones tipo letrero, sin caja contenedora.

Todo en Assets/Sprits/MenuLetreros, pixel art con Point filter; los 9-slices van a x1 (cada píxel del sprite es un
píxel de la pantalla a 1920x1080) para que los bordes de 2 y 3 px queden nítidos:
  - boton_normal.png / boton_normal_hover.png (9-slice, borde 2): fondo #2A1F40 al 85% y borde rosa #FF6FA5 (dorado #FFD166
    al pasar el ratón o con el teclado).
  - boton_principal.png / boton_principal_hover.png (9-slice, borde 6): "Iniciar juego", borde rosa de 3 px con un
    halo suave de 2 px por fuera (dorados al pasar).
  - campo.png (9-slice, borde 2): el campo del nombre, #2A1F40 con borde morado #8C52FF de 2 px.
  - placa.png (9-slice, borde 2): la placa de "TU NOMBRE" (#2A1F40 al 85%, sin borde).
  - panel.png (9-slice, borde 3): el panel de instrucciones, #2A1F40 al 94% con borde rosa de 3 px.
  - boton_peligro.png / boton_peligro_hover.png (9-slice, borde 2): "Salir" y "Reiniciar progreso", borde rojo #FF5D73.
  - marco_icono.png / marco_icono_hover.png (9-slice, borde 2): el marco de los botones cuadrados (iconos de 84 y 64).
  - marco_tarjeta.png / marco_tarjeta_hover.png (9-slice, borde 3, centro vacío): el borde de las tarjetas de nivel.
  - icono_musica.png (nota musical), icono_efectos.png (bocina), icono_pausa.png y raya_apagado.png (la raya diagonal
    roja #FF5D73 de apagado), 24x24 a x3 dentro del marco.
  - tijera.png (14x14, a x4 = 56): la tijera del letrero de neón, que parpadea a la izquierda del botón elegido.
  BotonBrenn (Assets/Scripts) y ConstruirBotones / ConstruirMenuPrincipal usan este arte en todo el juego.
  - degradado_menu.png (256x1, estirado): de transparente a #2A1F40 al 55%, detrás de la columna de botones.
  - HUD de los niveles (EstiloHUD): hud_caja.png (9-slice, borde 3: #2A1F40 al 90% con borde rosa de 3 px, la caja de
    pedidos, el dinero y el reloj), hud_marco.png (borde 2: la barra de satisfacción y el turbo), hud_reloj_verde /
    _amarilla / _roja.png (la placa del reloj con el borde del color del estado), hud_queja_vacia.png (18x18 a x3: el
    círculo con borde crema) y moneda.png (12x12 a x3, junto al dinero).
  - intro/menu_letreros_vista_previa.png: el menú sobre la vista previa del mirador, para revisar.
Uso: python generate_menu_letreros.py   (idempotente)
"""
import os
import sys

from PIL import Image, ImageDraw

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, 'intro'))
import generar_intro as g  # noqa: E402

DESTINO = os.path.join(g.SPRITS, 'MenuLetreros')
OSCURO = (0x2A, 0x1F, 0x40)
ROSA = (0xFF, 0x6F, 0xA5)
DORADO = (0xFF, 0xD1, 0x66)
DORADO_OSC = (0xE0, 0x9A, 0x3A)
MORADO = (0x8C, 0x52, 0xFF)
CREMA = (0xFF, 0xF1, 0xD0)
ROJO = (0xFF, 0x5D, 0x73)
VERDE = (0x7B, 0xD8, 0x8F)
FONDO_85 = 217                # 85% de 255
FONDO_PANEL = 240
FONDO_90 = 230                # 90% de 255 (el HUD)
DEGRADADO_55 = 140            # 55% de 255

# columna del menú a 1920x1080 (para la vista previa; Unity usa los mismos números en ConstruirMenuPrincipal)
COLUMNA_X, ANCHO = 1500, 360
DEGRADADO_DESDE = 1150


def caja(w, h, fondo, alfa, borde, grosor, halo=None):
    """Rectángulo con su borde de 'grosor' px y, si hay halo, 2 px de resplandor por fuera (alfa 110 y 50)."""
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    m = 0
    if halo:
        d.rectangle((0, 0, w - 1, h - 1), fill=halo + (50,))
        d.rectangle((1, 1, w - 2, h - 2), fill=halo + (110,))
        m = 2
    if borde:
        d.rectangle((m, m, w - 1 - m, h - 1 - m), fill=borde + (255,))
        m += grosor
    d.rectangle((m, m, w - 1 - m, h - 1 - m), fill=fondo + (alfa,))
    return im


def marco_icono(borde):
    return caja(12, 12, OSCURO, FONDO_85, borde, 2)


def marco_tarjeta(borde):
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    ImageDraw.Draw(im).rectangle((0, 0, 15, 15), outline=borde + (255,), width=3)
    return im


def pausa():
    """Dos barras de pausa crema."""
    im = Image.new('RGBA', (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle((7, 6, 10, 17), fill=CREMA + (255,))
    d.rectangle((13, 6, 16, 17), fill=CREMA + (255,))
    return im


def nota():
    """Dos corcheas unidas (crema con su sombra dorada), centradas en 24x24."""
    im = Image.new('RGBA', (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = CREMA + (255,)
    d.rectangle((9, 6, 18, 7), fill=c)                 # la barra que las une
    d.rectangle((9, 6, 10, 15), fill=c)                # las dos plicas
    d.rectangle((17, 6, 18, 14), fill=c)
    d.ellipse((5, 14, 10, 18), fill=c)                 # las cabezas
    d.ellipse((13, 13, 18, 17), fill=c)
    d.point((6, 15), fill=DORADO + (255,))
    d.point((14, 14), fill=DORADO + (255,))
    return im


def bocina():
    """Bocina con dos ondas de sonido."""
    im = Image.new('RGBA', (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = CREMA + (255,)
    d.rectangle((4, 9, 7, 14), fill=c)
    d.polygon([(8, 9), (12, 5), (12, 18), (8, 14)], fill=c)
    d.point((5, 10), fill=DORADO + (255,))
    d.arc((11, 8, 15, 15), -60, 60, fill=c)
    d.arc((11, 5, 19, 18), -55, 55, fill=c)
    return im


def raya():
    """La raya diagonal roja de apagado, con un contorno oscuro para que se lea sobre el icono."""
    im = Image.new('RGBA', (24, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    for k in range(3, 21):
        x, y = k, 23 - k
        for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1), (1, -1), (-1, 1), (2, 0), (0, 2), (-2, 0), (0, -2)):
            if im.getpixel((x + dx, y + dy))[3] == 0:
                im.putpixel((x + dx, y + dy), OSCURO + (255,))
    for k in range(3, 21):
        x, y = k, 23 - k
        for dx, dy in ((0, 0), (1, 0), (0, 1), (1, 1)):
            im.putpixel((x + dx - 1, y + dy - 1), ROJO + (255,))
    return im


def tijera():
    """La tijera del letrero de neón (12x12) con contorno oscuro: 14x14."""
    n = 14
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    x, y = 1, 1
    d.line([(x + 1, y), (x + 8, y + 7)], fill=DORADO + (255,), width=1)
    d.line([(x + 2, y), (x + 9, y + 7)], fill=DORADO + (255,), width=1)
    d.line([(x + 10, y), (x + 3, y + 7)], fill=DORADO + (255,), width=1)
    d.line([(x + 9, y), (x + 2, y + 7)], fill=DORADO + (255,), width=1)
    d.point((x + 5, y + 4), fill=DORADO_OSC + (255,))
    d.point((x + 6, y + 4), fill=DORADO_OSC + (255,))
    d.ellipse((x, y + 7, x + 4, y + 11), outline=DORADO + (255,))
    d.ellipse((x + 7, y + 7, x + 11, y + 11), outline=DORADO + (255,))
    px = im.load()
    out = im.copy()
    for yy in range(n):
        for xx in range(n):
            if px[xx, yy][3] == 0 and any(0 <= xx + a < n and 0 <= yy + b < n and px[xx + a, yy + b][3]
                                          for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                out.putpixel((xx, yy), OSCURO + (255,))
    return out


def queja_vacia():
    """El círculo de queja sin usar: borde crema de 1 px (3 a x3) y el centro oscuro, 18x18."""
    im = Image.new('RGBA', (18, 18), (0, 0, 0, 0))
    ImageDraw.Draw(im).ellipse((1, 1, 16, 16), fill=OSCURO + (FONDO_90,), outline=CREMA + (255,))
    return im


def moneda():
    """Moneda dorada con su canto oscuro, un aro adentro y un brillo crema, 12x12."""
    im = Image.new('RGBA', (12, 12), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.ellipse((0, 0, 11, 11), fill=DORADO + (255,), outline=DORADO_OSC + (255,))
    d.ellipse((3, 3, 8, 8), outline=DORADO_OSC + (255,))
    d.point((3, 2), fill=CREMA + (255,))
    d.point((2, 3), fill=CREMA + (255,))
    return im


def degradado():
    im = Image.new('RGBA', (256, 1))
    for x in range(256):
        im.putpixel((x, 0), OSCURO + (round(DEGRADADO_55 * x / 255),))
    return im


def sprites():
    return {
        'boton_normal': caja(12, 12, OSCURO, FONDO_85, ROSA, 2),
        'boton_normal_hover': caja(12, 12, OSCURO, FONDO_85, DORADO, 2),
        'boton_principal': caja(16, 16, OSCURO, FONDO_85, ROSA, 3, halo=ROSA),
        'boton_principal_hover': caja(16, 16, OSCURO, FONDO_85, DORADO, 3, halo=DORADO),
        'boton_peligro': caja(12, 12, OSCURO, FONDO_85, ROJO, 2),
        'boton_peligro_hover': caja(12, 12, OSCURO, FONDO_85, DORADO, 2),
        'campo': caja(12, 12, OSCURO, 255, MORADO, 2),
        'placa': caja(12, 12, OSCURO, 255, None, 0),
        'panel': caja(16, 16, OSCURO, FONDO_PANEL, ROSA, 3),
        'marco_icono': marco_icono(ROSA),
        'marco_icono_hover': marco_icono(DORADO),
        'icono_musica': nota(),
        'icono_efectos': bocina(),
        'icono_pausa': pausa(),
        'marco_tarjeta': marco_tarjeta(ROSA),
        'marco_tarjeta_hover': marco_tarjeta(DORADO),
        'raya_apagado': raya(),
        'tijera': tijera(),
        'degradado_menu': degradado(),
        'hud_caja': caja(12, 12, OSCURO, FONDO_90, ROSA, 3),
        'hud_marco': caja(12, 12, OSCURO, FONDO_90, ROSA, 2),
        'hud_reloj_verde': caja(12, 12, OSCURO, FONDO_90, VERDE, 3),
        'hud_reloj_amarilla': caja(12, 12, OSCURO, FONDO_90, DORADO, 3),
        'hud_reloj_roja': caja(12, 12, OSCURO, FONDO_90, ROJO, 3),
        'hud_queja_vacia': queja_vacia(),
        'moneda': moneda(),
    }


def nueve(im, w, h, b):
    """Estira un 9-slice de borde b a w x h (a x1), como lo hace Unity."""
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    sw, sh = im.size
    xs = [(0, b, 0, b), (b, sw - b, b, w - b), (sw - b, sw, w - b, w)]
    ys = [(0, b, 0, b), (b, sh - b, b, h - b), (sh - b, sh, h - b, h)]
    for x0, x1, dx0, dx1 in xs:
        for y0, y1, dy0, dy1 in ys:
            out.alpha_composite(im.crop((x0, y0, x1, y1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST), (dx0, dy0))
    return out


def vista_previa(s):
    base = os.path.join(AQUI, 'intro', 'mirador_vista_previa.png')
    lienzo = Image.open(base).convert('RGBA') if os.path.exists(base) else Image.new('RGBA', (1920, 1080), OSCURO + (255,))
    grad = s['degradado_menu'].resize((1920 - DEGRADADO_DESDE, 1080), Image.NEAREST)
    lienzo.alpha_composite(grad, (DEGRADADO_DESDE, 0))
    y = 540 - 230
    lienzo.alpha_composite(nueve(s['placa'], 520, 150, 2), (COLUMNA_X - 260, y))
    lienzo.alpha_composite(nueve(s['campo'], 488, 76, 2), (COLUMNA_X - 244, y + 58))
    y += 150 + 24
    for nombre, ancho, alto in (('boton_principal', 520, 108), ('boton_normal_hover', 480, 92), ('boton_peligro', 480, 92)):
        b = 6 if 'principal' in nombre else 2
        lienzo.alpha_composite(nueve(s[nombre], ancho, alto, b), (COLUMNA_X - ancho // 2, y))
        if nombre.endswith('hover'):
            lienzo.alpha_composite(s['tijera'].resize((56, 56), Image.NEAREST), (COLUMNA_X - ancho // 2 - 30 - 56, y + 18))
        y += alto + 24
    for k, (icono, apagado) in enumerate((('icono_musica', True), ('icono_efectos', False))):
        x = 1920 - 24 - 84 - (1 - k) * 104
        lienzo.alpha_composite(nueve(s['marco_icono'], 84, 84, 2), (x, 24))
        lienzo.alpha_composite(s[icono].resize((72, 72), Image.NEAREST), (x + 6, 30))
        if apagado:
            lienzo.alpha_composite(s['raya_apagado'].resize((72, 72), Image.NEAREST), (x + 6, 30))
    return lienzo


def main():
    os.makedirs(DESTINO, exist_ok=True)
    s = sprites()
    for n, im in s.items():
        if n not in ('degradado_menu',) and not n.startswith('boton_principal'):
            alfas = {a for a, k in enumerate(im.getchannel('A').histogram()) if k}
            assert alfas <= {0, FONDO_85, FONDO_PANEL, FONDO_90, 255}, (n, alfas)
        im.save(os.path.join(DESTINO, n + '.png'))
    vista_previa(s).save(os.path.join(AQUI, 'intro', 'menu_letreros_vista_previa.png'))
    print('ok', len(s), 'sprites en', DESTINO)


if __name__ == '__main__':
    main()
