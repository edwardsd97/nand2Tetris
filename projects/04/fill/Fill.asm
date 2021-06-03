// This file is part of www.nand2tetris.org
// and the book "The Elements of Computing Systems"
// by Nisan and Schocken, MIT Press.
// File name: projects/04/Fill.asm

// Runs an infinite loop that listens to the keyboard input.
// When a key is pressed (any key), the program blackens the screen,
// i.e. writes "black" in every pixel;
// the screen should remain fully black as long as the key is pressed. 
// When no key is pressed, the program clears the screen, i.e. writes
// "white" in every pixel;
// the screen should remain fully clear as long as no key is pressed.

// word_count = 8192 (256 * 32)
// word
// arr
// fill = 0

// while( 1 )
//  {
//		if ( KBD )
//
//	}

// init
	// word_count = 8192
	@8192
	D=A
	@word_count
	M=D

// while( 1 )
	(LOOP)

		@KBD
		D=M

		@CLEAR
		D;JEQ

		@FILL
		D;JNE

	@LOOP
	0;JMP

// Clear Screen
	(CLEAR)
		@fill
		M=0
		@FILL_VAL
		0;JMP

// Fill Screen
	(FILL)
		@fill
		M=-1

// Fill with fill_val
	(FILL_VAL)

		// word = 0
		@word
		M=0

		(FILL_LOOP)
			// if word == word_count goto LOOP
			@word_count
			D=M

			@word
			D=D-M

			@LOOP
			D;JEQ

			// addr = SCREEN + word
			@SCREEN
			D=A
			@addr
			M=D
			@word
			D=M
			@addr
			M=M+D

			// addr[word] = fill
			@fill
			D=M
			@addr
			A=M
			M=D

			// word++
			@word
			M=M+1

        @FILL_LOOP
		0;JMP




