"""Compara la captura de Unity (menú Delivery Brenn's > Capturar mapa Residencial) con la vista previa
del generador. Deja unity/residencial/comparacion_unity.png: arriba la vista previa, en medio la captura
y abajo las diferencias en rojo, todo a 1/2. Imprime el porcentaje de píxeles distintos por zona.

Las diferencias esperadas son los puntos de entrega (círculos rosas), el repartidor y los autos que se
mueven por código; un tile mal cortado o una curva desalineada se ve como un bloque rojo de 32x32."""
import os
import sys

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'unity', 'residencial')
T = 32

ref = np.asarray(Image.open(os.path.join(OUT, 'mapa_referencia_grande.png')).convert('RGB')).astype(int)
cap_path = os.path.join(OUT, 'captura_unity.png')
cap = np.asarray(Image.open(cap_path).convert('RGB')).astype(int)
if cap.shape != ref.shape:
    sys.exit(f'la captura mide {cap.shape[1]}x{cap.shape[0]} y la vista previa {ref.shape[1]}x{ref.shape[0]}')
if os.path.getmtime(cap_path) < os.path.getmtime(os.path.join(OUT, 'mapa_residencial_grande.json')):
    print('AVISO: la captura es más vieja que el mapa generado; vuelve a capturar en Unity.')

dist = np.abs(ref - cap).sum(axis=2) > 24          # tolerancia para redondeos de color
print(f'píxeles distintos: {dist.mean() * 100:.2f} %')
filas, cols = ref.shape[0] // T, ref.shape[1] // T
celdas = dist.reshape(filas, T, cols, T).mean(axis=(1, 3))
malas = np.argwhere(celdas > .5)
print(f'celdas con más de la mitad distinta: {len(malas)}', [tuple(int(v) for v in m) for m in malas[:20]])

marca = (ref // 2).astype(np.uint8)
marca[dist] = (255, 40, 40)
mitad = lambda a: Image.fromarray(a.astype(np.uint8)).resize((ref.shape[1] // 2, ref.shape[0] // 2), Image.NEAREST)
hoja = Image.new('RGB', (ref.shape[1] // 2, ref.shape[0] * 3 // 2))
for i, a in enumerate((ref, cap, marca)):
    hoja.paste(mitad(a), (0, i * ref.shape[0] // 2))
hoja.save(os.path.join(OUT, 'comparacion_unity.png'))
print('guardado', os.path.join(OUT, 'comparacion_unity.png'))
