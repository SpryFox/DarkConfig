import os
import sys
import random

num_configs = int(sys.argv[1])
start_value = int(sys.argv[2])

class ArtPos(object):
    def __init__(self, pos, size):
        self.Pos = pos
        self.Size = size

    def __str__(self):
        return "\n        Pos: [%f, %f]\n        Size: [%f, %f]\n" % (
            self.Pos[0], self.Pos[1], self.Size[0], self.Size[1])

for i in xrange(num_configs):
    body = dict()
    body["RotationRate"] = random.randint(40, 70)
    body["Speed"] = random.randint(5, 20)
    body["HitPoints"] = random.randint(3, 7)
    body["AIRange"] = 30
    body["Fuselage"] = ArtPos([0, 0], [random.uniform(0.8, 1.5), random.uniform(1, 2)])
    body["Wing"] = ArtPos([(body["Fuselage"].Size[0] - 0.8) * 0.5, random.uniform(-0.5, 0.7)],
                          [random.uniform(0.4, 1.5), random.uniform(0.6, 2)])
    body["Stabilizer"] = ArtPos([0, -body["Fuselage"].Size[1] * random.uniform(0.3, 1)],
                                [random.uniform(0.5, 2), random.uniform(0.5, 2)])
    body["GunMounts"] = "[{Name: Piddler, Location: [0, 0]}]"
    body["LootTable"] = "[{Weight: 2}, {Weight: 1, Health: 2}]"

    print "Generated%s:" % (i + start_value)
    for k, v in body.iteritems():
        print "    %s: %s" % (k, v)