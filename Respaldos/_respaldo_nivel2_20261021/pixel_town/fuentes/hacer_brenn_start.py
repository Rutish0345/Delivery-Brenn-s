"""Brenn Start: Press Start 2P con las mayúsculas acentuadas a tamaño completo.

Press Start 2P dibuja Á É Í Ó Ú Ñ Ü del alto de una minúscula para que el acento quepa en su cuadro de 8x8;
en los titulares se leía "SALóN". Aquí cada una se rearma con la mayúscula completa (O, A...) y el acento de
la fuente (acute, tilde, dieresis) encima, con un pixel de aire: el acento sobresale de la línea, pero las
métricas (ascent, alto de línea, avances) no cambian.
La licencia OFL de Press Start 2P reserva su nombre: la variante se llama "Brenn Start".
Uso: python hacer_brenn_start.py  ->  DeliveryBrenns/Assets/Fonts/Brenn_Start/BrennStart-Regular.ttf (+ OFL.txt)
"""
import os
import shutil

from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

PROYECTO = os.environ.get('PROYECTO_UNITY', r'C:\Users\kangu\DeliveryBrenns')
ORIGEN = os.path.join(PROYECTO, 'Assets', 'Fonts', 'Press_Start_2P')
DESTINO = os.path.join(PROYECTO, 'Assets', 'Fonts', 'Brenn_Start')
PIXEL = 125                       # 1000 unidades por em / 8 pixeles

# glifo -> (mayúscula base, acento)
COMPUESTOS = {
    'Aacute': ('A', 'acute'), 'Eacute': ('E', 'acute'), 'Iacute': ('I', 'acute'),
    'Oacute': ('O', 'acute'), 'Uacute': ('U', 'acute'), 'Ntilde': ('N', 'tilde'),
    'Udieresis': ('U', 'dieresis'),
}
NOMBRES = {1: 'Brenn Start', 3: '3.000;BRENN;BrennStart-Regular', 4: 'Brenn Start Regular', 6: 'BrennStart-Regular',
           5: 'Version 3.000; Brenn Start: Press Start 2P con mayusculas acentuadas completas'}


def caja(gs, nombre):
    p = BoundsPen(gs)
    gs[nombre].draw(p)
    return p.bounds


def main():
    f = TTFont(os.path.join(ORIGEN, 'PressStart2P-Regular.ttf'))
    gs = f.getGlyphSet()
    glyf = f['glyf']
    for nombre, (base, acento) in COMPUESTOS.items():
        tope = caja(gs, base)[3]                         # alto de la mayúscula
        dy = tope + PIXEL - caja(gs, acento)[1]          # el acento empieza un pixel arriba
        pen = TTGlyphPen(gs)
        gs[base].draw(pen)
        gs[acento].draw(TransformPen(pen, (1, 0, 0, 1, 0, dy)))
        glyf[nombre] = pen.glyph()
        assert f['hmtx'][nombre][0] == f['hmtx'][base][0], nombre
    for r in f['name'].names:
        if r.nameID in NOMBRES:
            r.string = NOMBRES[r.nameID]
    os.makedirs(DESTINO, exist_ok=True)
    salida = os.path.join(DESTINO, 'BrennStart-Regular.ttf')
    f.save(salida)
    shutil.copy(os.path.join(ORIGEN, 'OFL.txt'), os.path.join(DESTINO, 'OFL.txt'))
    print('Listo:', salida, '|', ', '.join(COMPUESTOS))


if __name__ == '__main__':
    main()
