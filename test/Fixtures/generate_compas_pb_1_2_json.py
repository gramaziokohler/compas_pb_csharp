"""Regenerate the deterministic Python-to-C# compas_pb 1.2 JSON fixture.

Run this with compas_pb 1.2 installed. `pb_dump_json` is the upstream JSON entry point;
the C# runtime has to read exactly what it writes, defaults omitted and all.
"""

from pathlib import Path

from compas.geometry import Frame
from compas.geometry import Point
from compas.geometry import Vector
from compas_pb import pb_dump_json


payload = {
    "frame": Frame(Point(1.0, 2.0, 3.0), Vector(1.0, 0.0, 0.0), Vector(0.0, 1.0, 0.0)),
    "count": 0,  # a whole number sitting at its default value
    "ratio": 0.0,  # a float sitting at its default value
    "label": "",  # an empty string
    "flag": False,
    "items": [1, 2.0, "x"],
}

Path(__file__).with_name("compas_pb_1_2_payload.json").write_text(pb_dump_json(payload) + "\n")
