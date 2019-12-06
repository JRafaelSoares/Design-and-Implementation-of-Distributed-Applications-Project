#!/usr/bin/python

import os
import shutil


num_clients = 12

test_names = ["create-one-server", "create-two-servers", "create-three-servers", "join-one-server", \
        "join-two-servers", "join-three-servers", "list-one-server", "list-two-servers", "list-three-servers", \
        "close-one-server", "close-two-servers", "close-three-servers"]



''' create client files'''
for test_name in test_names:
    os.makedirs("test-scripts/client/" + test_name)
    for i in range(num_clients):
        f = open("test-scripts/client/" + test_name + "/client-" + str(i) + ".txt", "w")
        f.close()