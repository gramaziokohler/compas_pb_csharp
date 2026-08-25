"""Regenerate the deterministic Python-to-C# compas_pb 1.1 fixture.

Run this with compas_pb 1.1 installed. The fixture is deliberately pinned to 1.1 so the
C# tests keep proving that a payload from an older minor version still reads.
"""

from pathlib import Path
from uuid import UUID
import base64

from compas.geometry import Frame
from compas.geometry import Transformation
from compas_model.elements import Element
from compas_model.models import Model
from compas_pb import pb_dump_bts


element = Element(
    transformation=Transformation.from_frame(Frame([1, 2, 3], [1, 0, 0], [0, 1, 0])),
    name="QR_0",
)
element._guid = UUID("22222222-2222-2222-2222-222222222222")

model = Model()
model._guid = UUID("11111111-1111-1111-1111-111111111111")
model.add_element(element)

encoded = base64.b64encode(pb_dump_bts(model)).decode("ascii")
Path(__file__).with_name("compas_pb_1_1_model.b64").write_text(encoded + "\n")
