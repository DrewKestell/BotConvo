import gpt_2_simple as gpt2
import os
import random
import decimal
import sys
from http.server import HTTPServer, BaseHTTPRequestHandler

model_name = "124M"
checkpoint_dir = sys.argv[1]
model_dir = sys.argv[2]
checkpoint_name = sys.argv[3]
port = sys.argv[4]

message_counter = 0
sess = gpt2.start_tf_sess()
gpt2.load_gpt2(sess, checkpoint_dir=checkpoint_dir, run_name=checkpoint_name)


class SimpleHTTPRequestHandler(BaseHTTPRequestHandler):

    def do_GET(self):
        global checkpoint_dir
        global checkpoint_name
        global message_counter
        global sess

        message_counter += 1

        if message_counter == 30:
            gpt2.reset_session(sess)
            sess = gpt2.start_tf_sess()
            gpt2.load_gpt2(sess, checkpoint_dir=checkpoint_dir, run_name=checkpoint_name)
            message_counter = 0

        prompt = self.headers.get('prompt')
        length = random.randrange(30, 100)
        temperature = float(decimal.Decimal(random.randrange(60, 80)) / 100)
        prefix = '<|startoftext|>';
        if prompt:
            prefix += prompt

        text = gpt2.generate(sess,
                             checkpoint_dir=checkpoint_dir,
                             run_name=checkpoint_name,
                             length=length,
                             temperature=temperature,
                             prefix=prefix,
                             truncate='<|endoftext|>',
                             return_as_list=True,
                             nsamples=5,
                             batch_size=5
                             )
        sorted_list = sorted(text, key=len)

        self.send_response(200)
        self.end_headers()
        self.wfile.write(str.encode(sorted_list[4]))

    def log_message(self, format, *args):
        return


httpd = HTTPServer(('localhost', int(port)), SimpleHTTPRequestHandler)
httpd.serve_forever()
