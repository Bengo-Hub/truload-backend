import sys

try:
    import PyPDF2

    def extract_text(pdf_path):
        text = ""
        with open(pdf_path, 'rb') as file:
            reader = PyPDF2.PdfReader(file)
            for page in reader.pages:
                page_text = page.extract_text()
                if page_text:
                    text += page_text + "\n"
        return text

    print(extract_text(sys.argv[1]))
except ImportError:
    print("PyPDF2 not installed. Use pip install PyPDF2")
