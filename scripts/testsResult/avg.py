#!/usr/bin/python

test_folders = ["list-one-server", "list-two-server", "list-three-server", "join-one-server", "join-two-server", "join-three-server", "join-three-server-2faults", "create-one-server", "create-two-server",\
                 "create-three-server", "create-three-server-2faults", "create-one-server-nogossip", "create-two-server-nogossip", \
                 "create-three-servers-nogossip", "create-three-server-2faults-nogossip"]

for folder  in test_folders:
    avg = 0.0
    num_lines = 0    
    for num_client in range(1,13):
        with open(folder + "/" + "client-" + str(num_client) + ".txtc" + str(num_client) + "results.txt", "r") as f:
            for line in f:
                avg += eval(line)
                num_lines += 1
    avg /= num_lines
    print(f"{folder} : {avg}")